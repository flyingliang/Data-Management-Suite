﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using FalconSoft.ReactiveWorksheets.Client.SignalR;
using FalconSoft.ReactiveWorksheets.Common;
using FalconSoft.ReactiveWorksheets.Common.Facade;
using FalconSoft.ReactiveWorksheets.Common.Metadata;
using FalconSoft.ReactiveWorksheets.Common.Security;
using FalconSoft.ReactiveWorksheets.InProcessServer.Client;

namespace ReactiveWorksheets.Facade.Tests
{
    internal class Program
    {
        private static IFacadesFactory _facadesFactory;
        private const string ConnectionString = @"http://192.168.0.15:8081";
        private static ISecurityFacade _securityFacade;
        
        private static void Main()
        {
            _facadesFactory = GetFacadesFactory("InProcess");
            
            _securityFacade = _facadesFactory.CreateSecurityFacade();
          

            var datasource = TestDataFactory.CreateTestDataSourceInfo();
            var user = TestDataFactory.CreateTestUser();

            Console.WriteLine("Testing starts...");
           
            user = TestSecurityfacade(user);

            Console.WriteLine("\n1. Create DataSource (Customers)");
            var dataSourceTest = new SimpleDataSourceTest(datasource, _facadesFactory, user);

            Console.WriteLine("\n2. Create 3 different worksheets referencing to the same data source with three different columns set and filter rules");
            var firstWorksheetColumns = new List<ColumnInfo>
            {
                new ColumnInfo(datasource.Fields["CustomerID"]),
                new ColumnInfo(datasource.Fields["CompanyName"]),
                new ColumnInfo(datasource.Fields["ContactName"])
            };
            var firstWorksheet = dataSourceTest.CreateWorksheetInfo("First Worksheet", "Test", firstWorksheetColumns, user);
            
            var secondWorksheetColumns = new List<ColumnInfo>
            {
                new ColumnInfo(datasource.Fields["CustomerID"]),
                new ColumnInfo(datasource.Fields["ContactTitle"]),
                new ColumnInfo(datasource.Fields["Address"])
            };
            var secondWorksheet = dataSourceTest.CreateWorksheetInfo("Second Worksheet", "Test", secondWorksheetColumns, user);
            var thirdWorksheetColumns =  new List<ColumnInfo>
            {
                new ColumnInfo(datasource.Fields["CustomerID"]),
                new ColumnInfo(datasource.Fields["City"]),
                new ColumnInfo(datasource.Fields["Region"])
            };
            var thirdWorksheet = dataSourceTest.CreateWorksheetInfo("Third Worksheet", "Test", thirdWorksheetColumns, user);

            Console.WriteLine("\n3. Submit data (from Tsv)");
            var data = TestDataFactory.CreateTestData().ToArray();

            dataSourceTest.SubmitData("test data save", data);

            Console.WriteLine("\n4. GetData");
            var getData = dataSourceTest.GetData();
            Console.WriteLine("Saved data count : {0}",getData.Count());

            Console.WriteLine("\n5. Subscribe on changes and submit a few modified rows. Make sure you get proper updates.");
            dataSourceTest.GetDataChanges().Subscribe(GetDataChanges);

            data[0]["CompanyName"] = "New value";
            data[1]["CompanyName"] = "New value";
            data[2]["CompanyName"] = "New value";

            dataSourceTest.SubmitData("Make changes",data.Take(3));

            Console.WriteLine("\n6. Check history for modified records");
            var firstRecordHistory = dataSourceTest.GetHistory(datasource.GetKeyFieldsName().Aggregate("", (cur, key) => cur + "|" + data[0][key]));
            Console.WriteLine("First Record History count : {0}",firstRecordHistory.Count());
            var secondRecordHistory = dataSourceTest.GetHistory(datasource.GetKeyFieldsName().Aggregate("", (cur, key) => cur + "|" + data[0][key]));
            Console.WriteLine("First Record History count : {0}", secondRecordHistory.Count());
            var thirdtRecordHistory = dataSourceTest.GetHistory(datasource.GetKeyFieldsName().Aggregate("", (cur, key) => cur + "|" + data[0][key]));
            Console.WriteLine("First Record History count : {0}", thirdtRecordHistory.Count());

            Console.WriteLine("\n7. Make changes to DataSourcenfo add fields");
            var addField = new FieldInfo
            {
                DataSourceProviderString = "Customers\\Northwind",
                DataType = DataTypes.String,
                DefaultValue = null,
                IsKey = false,
                IsNullable = true,
                IsParentField = false,
                IsReadOnly = false,
                IsSearchable = true,
                IsUnique = false,
                Name = "NewField",
                RelatedFieldName = null,
                RelationUrn = null,
                Size = null
            };

            datasource.Fields.Add(addField.Name,addField);
            datasource = dataSourceTest.UpdateDataSourceInfo(datasource, user);

            Console.WriteLine("\n8. Get Data and see what is there");
            getData = dataSourceTest.GetData();
            Console.WriteLine("Updated dataSource data count : {0}", getData.Count());
            var updatedDatasourceKeys = getData.First().Keys;
            Console.WriteLine("Data keys {0}",updatedDatasourceKeys.Aggregate("",(cur,key)=>cur + " : ["+key+"]"));
            Console.WriteLine("Datasource field keys {0}", datasource.Fields.Keys.Aggregate("", (cur, key) => cur + " : [" + key + "]"));
            
            Console.WriteLine("\n7. Make changes to DataSourcenfo remove fields");
            datasource.Fields.Remove("Region");

            datasource = dataSourceTest.UpdateDataSourceInfo(datasource, user);
            Console.WriteLine("\n8. Get Data and see what is there");
            getData = dataSourceTest.GetData();
            Console.WriteLine("Updated dataSource data count : {0}", getData.Count());
            updatedDatasourceKeys = getData.First().Keys;
            Console.WriteLine("Data keys {0}", updatedDatasourceKeys.Aggregate("", (cur, key) => cur + " : [" + key + "]"));
            Console.WriteLine("Datasource field keys {0}", datasource.Fields.Keys.Aggregate("", (cur, key) => cur + " : [" + key + "]"));
            
            Console.WriteLine("\n9. Delete records");
            dataSourceTest.RemoveWorksheet(firstWorksheet,user);
            dataSourceTest.RemoveWorksheet(secondWorksheet, user);
            dataSourceTest.RemoveWorksheet(thirdWorksheet, user);
            var keyFields = datasource.GetKeyFieldsName();
            var datakeys = data.Select(record => keyFields.Aggregate("", (cur, key) => cur + "|" + record[key]));
            dataSourceTest.SubmitData("Remove test data", null, datakeys);
            dataSourceTest.RemoveDatasourceInfo(user);
            dataSourceTest.Dispose();

            Console.WriteLine("Test finish. Type <Enter> to exit.");
            Console.ReadLine();
        }

        private static void GetDataChanges(RecordChangedParam obj)
        {
            Console.WriteLine("RecordChangedParam resived RecordKey : {0} OriginalRecordKey : {1} dataDourcePath : {2}", obj.RecordKey, obj.OriginalRecordKey, obj.ProviderString);
        }

        private static User TestSecurityfacade(User user)
        {
            Console.WriteLine("Step #1. Create test user.");
            _securityFacade.SaveNewUser(user);

            Console.WriteLine("Cheacking if user is created...");
            var allUsers = _securityFacade.GetUsers();
            if (allUsers.Exists(u => u.LoginName == user.LoginName))
            {
                Console.WriteLine("Insert successfull");
                user = allUsers.FirstOrDefault(u => u.LoginName == user.LoginName);
            }
            return user;
        }

        private static IFacadesFactory  GetFacadesFactory(string facadeType)
        {
            if (facadeType.Equals("SignalR", StringComparison.OrdinalIgnoreCase))
            {
                return new SignalRFacadesFactory(ConnectionString);
            }
            if (facadeType.Equals("InProcess", StringComparison.OrdinalIgnoreCase))
            {
                // this is hardcode for testing only
                const string metaDataPersistenceConnectionString = "mongodb://localhost/rw_metadata";
                const string persistenceDataConnectionString = "mongodb://localhost/rw_data";
                const string mongoDataConnectionString = "mongodb://localhost/MongoData";

                return new InProcessServerFacadesFactory(metaDataPersistenceConnectionString, persistenceDataConnectionString, mongoDataConnectionString);
            }
            throw new ConfigurationException("Unsupported facade type - >" + facadeType);
        }
    }
}

