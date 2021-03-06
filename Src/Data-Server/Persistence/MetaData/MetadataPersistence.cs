﻿using System;
using System.Linq;
using FalconSoft.Data.Management.Common;
using FalconSoft.Data.Management.Common.Metadata;
using FalconSoft.Data.Management.Common.Security;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace FalconSoft.Data.Server.Persistence.MetaData
{
    public class MetaDataPersistence : IMetaDataPersistence
    {
        readonly string _connectionString;

        private MongoDatabase _mongoDatabase;

        private const string DataSourceCollectionName = "MetaData_DataSourceInfo";

        private string _dbState;

        public MetaDataPersistence(string connectionString)
        {
            _connectionString = connectionString;
        }

        private void ConnectToDb()
        {
            if (_mongoDatabase != null)
            {
                if (_dbState != _mongoDatabase.Server.State.ToString())
                {
                    Console.WriteLine("  Database MongoDB status {0}", _mongoDatabase.Server.State);
                    _dbState = _mongoDatabase.Server.State.ToString();
                }
            }
            if (_mongoDatabase == null || _mongoDatabase.Server.State != MongoServerState.Connected)
            {
                _mongoDatabase = MongoDatabase.Create(_connectionString);
            }
        }

        public void ClearAllMetaData()
        {
            ConnectToDb();
            _mongoDatabase.GetCollection<DataSourceInfo>(DataSourceCollectionName).RemoveAll();
        }

        public DataSourceInfo[] GetAvailableDataSources()
        {
            ConnectToDb();
            return _mongoDatabase.GetCollection<DataSourceInfo>(DataSourceCollectionName).FindAll().ToArray();
        }

        public DataSourceInfo GetDataSourceInfo(string dataSourceProviderString)
        {
            ConnectToDb();
            var allds = _mongoDatabase.GetCollection<DataSourceInfo>(DataSourceCollectionName);
            return allds.FindOne(Query.And(Query.EQ("Name", dataSourceProviderString.GetName()),
                                       Query.EQ("Category", dataSourceProviderString.GetCategory())));
        }

        public void UpdateDataSourceInfo(DataSourceInfo dataSource, string oldDataSourceProviderString, string userId)
        {
            ConnectToDb();
            var collection = _mongoDatabase.GetCollection<DataSourceInfo>(DataSourceCollectionName);
            var oldDs = collection.FindOneAs<DataSourceInfo>(Query.And(Query.EQ("Name", oldDataSourceProviderString.GetName()),
                                                                       Query.EQ("Category", oldDataSourceProviderString.GetCategory())));
            var childDataSources = oldDataSourceProviderString.GetChildDataSources(collection.FindAll().ToArray());
            if (dataSource.DataSourcePath != oldDs.DataSourcePath)
            {
                var oldCollNameData = oldDs.DataSourcePath.ToValidDbString() + "_Data";
                var oldCollNameHistory = oldDs.DataSourcePath.ToValidDbString() + "_History";
                _mongoDatabase.RenameCollection(oldCollNameData, dataSource.DataSourcePath.ToValidDbString() + "_Data");
                _mongoDatabase.RenameCollection(oldCollNameHistory, dataSource.DataSourcePath.ToValidDbString() + "_History");
            }
            var dataCollection = _mongoDatabase.GetCollection(dataSource.DataSourcePath.ToValidDbString() + "_Data");
            var historyCollection = _mongoDatabase.GetCollection(dataSource.DataSourcePath.ToValidDbString() + "_History");
            //IF NEW FIELDS ADDED  (ONLY)  
            var addedfields = dataSource.Fields.Keys.Except(oldDs.Fields.Keys).ToList();
            foreach (var addedfield in addedfields)
            {
                dataCollection.Update(Query.Null, Update.Set(addedfield, string.Empty), UpdateFlags.Multi);
                historyCollection.Update(Query.Null, Update.Set(addedfield, string.Empty), UpdateFlags.Multi);
            }
            //IF FIELDS REMOVED  (ONLY)
            var removedfields = oldDs.Fields.Keys.Except(dataSource.Fields.Keys).ToList();
            foreach (var removedfield in removedfields)
            {
                dataCollection.Update(Query.Null, Update.Unset(removedfield), UpdateFlags.Multi);
                historyCollection.Update(Query.Null, Update.Unset(removedfield), UpdateFlags.Multi);
            }
            dataSource.Id = oldDs.Id;
            oldDs.Update(dataSource);
            collection.Save(oldDs);

            //WE NEED TO MODIFY ALL CHILD DATASOURCES
            foreach (var childDataSource in childDataSources)
            {
                var childDataCollection = _mongoDatabase.GetCollection(childDataSource.DataSourcePath.ToValidDbString() + "_Data");
                var childHistoryCollection = _mongoDatabase.GetCollection(childDataSource.DataSourcePath.ToValidDbString() + "_History");
                foreach (var addedfield in addedfields)
                {
                    childDataCollection.Update(Query.Null, Update.Set(addedfield, string.Empty), UpdateFlags.Multi);
                    childHistoryCollection.Update(Query.Null, Update.Set(addedfield, string.Empty), UpdateFlags.Multi);
                }
                foreach (var removedfield in removedfields)
                {
                    childDataCollection.Update(Query.Null, Update.Unset(removedfield), UpdateFlags.Multi);
                    childHistoryCollection.Update(Query.Null, Update.Unset(removedfield), UpdateFlags.Multi);
                }
                var childDs = (DataSourceInfo)childDataSource.Clone();
                foreach (var fieldKey in childDataSource.Fields.Where(x => x.Value.DataSourceProviderString == oldDataSourceProviderString))
                {
                    childDs.Fields.Remove(fieldKey.Key);
                }
                foreach (var field in oldDs.Fields.Values)
                {
                    var f = (FieldInfo)field.Clone();
                    f.IsParentField = true;
                    childDs.Fields.Add(f.Name, f);
                }
                collection.Save(childDs);
            }
        }

        public DataSourceInfo CreateDataSourceInfo(DataSourceInfo dataSource, string userId)
        {
            ConnectToDb();
            var collection = _mongoDatabase.GetCollection<DataSourceInfo>(DataSourceCollectionName);
            dataSource.Id = Convert.ToString(ObjectId.GenerateNewId());
            collection.Insert(dataSource);
            return collection.FindOneAs<DataSourceInfo>(Query.And(Query.EQ("Name", dataSource.DataSourcePath.GetName()),
                                                                  Query.EQ("Category", dataSource.DataSourcePath.GetCategory())));
        }

        public void DeleteDataSourceInfo(string dataSourceProviderString, string userId)
        {
            ConnectToDb();
            _mongoDatabase.GetCollection(dataSourceProviderString.ToValidDbString() + "_Data").Drop();
            _mongoDatabase.GetCollection(dataSourceProviderString.ToValidDbString() + "_History").Drop();
            _mongoDatabase.GetCollection(DataSourceCollectionName).Remove(Query.And(Query.EQ("Name", dataSourceProviderString.GetName()),
                                                                                    Query.EQ("Category", dataSourceProviderString.GetCategory())));
        }
    }
}
