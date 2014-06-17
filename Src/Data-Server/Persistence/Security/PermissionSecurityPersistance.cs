﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FalconSoft.Data.Management.Common;
using FalconSoft.Data.Management.Common.Security;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Driver.Wrappers;

namespace FalconSoft.Data.Server.Persistence.Security
{
    public class PermissionSecurityPersistance : IPermissionSecurityPersistance
    {
        private readonly string _connectionString;
        private const string PermissionsCollectionName = "Permissions";
        private MongoDatabase _mongoDatabase;

        public PermissionSecurityPersistance(string connectionString)
        {
            _connectionString = connectionString;
            ConnectToDb();
            if (!_mongoDatabase.CollectionExists(PermissionsCollectionName))
            {
                _mongoDatabase.CreateCollection(PermissionsCollectionName);
               
                _mongoDatabase.GetCollection<User>("Users").Insert(new User
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    LoginName = "Admin",
                    FirstName = "Ivan",
                    LastName = "Ivanov",
                    Password = "Admin"
                });
                var user = _mongoDatabase.GetCollection<User>("Users").FindOne(Query<User>.EQ(u => u.LoginName, "Admin"));
                if (user != null)
                {
                    var collection = _mongoDatabase.GetCollection<Permission>(
                   PermissionsCollectionName);
                    collection.Insert(new Permission
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        UserRole = UserRole.Administrator,
                        UserId = user.Id,
                        DataSourceAccessPermissions = new Dictionary<string, DataSourceAccessPermission>
                        {
                            {
                                "ExternalDataSource\\QuotesFeed",
                               new DataSourceAccessPermission{AccessLevel = AccessLevel.DataModify | AccessLevel.MetaDataModify | AccessLevel.Read,GrantedByUserId = user.Id}
                            },
                            {
                                "ExternalDataSource\\MyTestData",
                                 new DataSourceAccessPermission{AccessLevel = AccessLevel.DataModify | AccessLevel.MetaDataModify | AccessLevel.Read,GrantedByUserId = user.Id}
                            },
                            {
                                "ExternalDataSource\\Calculator",
                                new DataSourceAccessPermission{AccessLevel = AccessLevel.DataModify | AccessLevel.MetaDataModify | AccessLevel.Read,GrantedByUserId = user.Id}
                            },
                            {
                                "ExternalDataSource\\YahooEquityRefData",
                                 new DataSourceAccessPermission{AccessLevel = AccessLevel.DataModify | AccessLevel.MetaDataModify | AccessLevel.Read,GrantedByUserId = user.Id}
                            },
                        }
                    });
                }
            }
        }

        private void ConnectToDb()
        {
            if (_mongoDatabase == null || _mongoDatabase.Server.State != MongoServerState.Connected)
            {
                _mongoDatabase = MongoDatabase.Create(_connectionString);
            }
        }

        public Permission GetUserPermissions(string userToken)
        {
            ConnectToDb();

            var collection = _mongoDatabase.GetCollection(typeof (Permission),
                PermissionsCollectionName);

            var permission = collection.FindAllAs<Permission>().FirstOrDefault(p => p.UserId == userToken);
            if (permission != null)
            {
                return permission;
            }
            return null;
        }

        public void SaveUserPermissions(Dictionary<string, AccessLevel> permissions, string targetUserToken, string grantedByUserToken, Action<string> messageAction = null)
        {
            ConnectToDb();
            try
            {
                var collection = _mongoDatabase.GetCollection<Permission>(PermissionsCollectionName);
                var permission = collection.FindOneAs<Permission>(Query<Permission>.EQ(p=>p.UserId,targetUserToken));
                if (permission != null)
                {
                    var permissionsCollection = permission.DataSourceAccessPermissions;
                    foreach (var accessLevel in permissions)
                    {
                        if (permissionsCollection.ContainsKey(accessLevel.Key))
                        {
                            permissionsCollection[accessLevel.Key].AccessLevel =
                                accessLevel.Value;
                            permissionsCollection[accessLevel.Key].GrantedByUserId = grantedByUserToken;
                        }
                        else
                        {
                            permissionsCollection.Add(accessLevel.Key, new DataSourceAccessPermission
                            {
                                AccessLevel = accessLevel.Value,
                                GrantedByUserId = grantedByUserToken
                            });
                        }
                    }
                    collection.Update(Query<Permission>.EQ(p => p.UserId, targetUserToken),
                        Update<Permission>.Set(p => p.DataSourceAccessPermissions, permissionsCollection));
                }
                else
                {
                    collection.Insert(new Permission
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        UserId = targetUserToken,
                        DataSourceAccessPermissions = permissions.ToDictionary(p => p.Key, p => new DataSourceAccessPermission { AccessLevel = p.Value, GrantedByUserId = grantedByUserToken })
                    });
                }
                if (messageAction != null)
                    messageAction("Permissions saved successful");
            }
            catch (Exception ex)
            {
                if (messageAction != null)
                    messageAction("Permissions saving failed ! \nError message : " + ex.Message);
                throw;
            }
        }


        public void ChangeUserRole(string userToken, UserRole userRole)
        {
            var collection = _mongoDatabase.GetCollection<Permission>(PermissionsCollectionName);
            var permission = collection.FindOneAs<Permission>(Query<Permission>.EQ(p => p.UserId, userToken));
            if (permission != null)
            {
                collection.Update(Query<Permission>.EQ(p => p.UserId, userToken),
                    Update<Permission>.Set(p => p.UserRole, userRole));
            }
            else
            {
                collection.Insert(new Permission
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    UserId = userToken,
                    UserRole = userRole,
                    DataSourceAccessPermissions = new Dictionary<string, DataSourceAccessPermission>()
                });
            }
        }
    }
}
