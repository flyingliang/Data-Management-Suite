﻿using System;
using System.Collections.Generic;
using System.Linq;
using FalconSoft.Data.Management.Common;
using FalconSoft.Data.Management.Common.Security;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

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
            if (_mongoDatabase.CollectionExists(PermissionsCollectionName)) return;
            _mongoDatabase.CreateCollection(PermissionsCollectionName);

            CreatePowerAdmin("Admin", "Admin");
            CreatePowerAdmin("consoleClient", "console");
        }

        private void CreatePowerAdmin(string userName, string password)
        {
            var id = ObjectId.GenerateNewId().ToString();
            _mongoDatabase.GetCollection<User>("Users").Insert(new User
            {
                Id = id,
                LoginName = userName,
                Password = password
            });

            var collection = _mongoDatabase.GetCollection<Permission>(PermissionsCollectionName);
            collection.Insert(new Permission
            {
                Id = ObjectId.GenerateNewId().ToString(),
                UserId = id,
                UserRole = UserRole.Administrator,
                DataSourceAccessPermissions = new Dictionary<string, DataSourceAccessPermission>()
            });
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
            var collection = _mongoDatabase.GetCollection(typeof (Permission), PermissionsCollectionName);
            return collection.FindAllAs<Permission>().FirstOrDefault(p => p.UserId == userToken);
        }

        public void SaveUserPermissions(Dictionary<string, AccessLevel> permissions, string targetUserToken,
            string grantedByUserToken, Action<string> messageAction = null)
        {
            try
            {
                var collection = _mongoDatabase.GetCollection<Permission>(PermissionsCollectionName);
                var permission = collection.FindOneAs<Permission>(Query<Permission>.EQ(p => p.UserId, targetUserToken));
                if (permission != null)
                {
                    var permissionsCollection = permission.DataSourceAccessPermissions;
                    foreach (var accessLevel in permissions)
                    {
                        if (permissionsCollection.ContainsKey(accessLevel.Key))
                        {
                            if (accessLevel.Value == 0)
                            {
                                permissionsCollection.Remove(accessLevel.Key);
                            }
                            else
                            {
                                permissionsCollection[accessLevel.Key].AccessLevel = accessLevel.Value;
                                permissionsCollection[accessLevel.Key].GrantedByUserId = grantedByUserToken;
                            }
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

                    if (NotifyPermissionChanged != null)
                        NotifyPermissionChanged(this, new PermissionEventArgs(targetUserToken, permissions));
                }
                else
                {
                    permission = new Permission
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        UserId = targetUserToken,
                        DataSourceAccessPermissions = permissions.Where(p => p.Value > (AccessLevel) 1)
                            .ToDictionary(p => p.Key,
                                p =>
                                    new DataSourceAccessPermission
                                    {
                                        AccessLevel = p.Value,
                                        GrantedByUserId = grantedByUserToken
                                    })
                    };
                    collection.Insert(permission);

                    if (NotifyPermissionChanged != null)
                        NotifyPermissionChanged(this, new PermissionEventArgs(targetUserToken, permissions));
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

        public void ChangeUserRole(string userToken, UserRole userRole, string grantedByUserToken)
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

        public AccessLevel CheckAccess(string userToken, string urn)
        {
            var permission = GetUserPermissions(userToken);

            if (permission != null)
            {
                if (permission.IsAdministrator)
                    return (AccessLevel) 7;

                if (permission.DataSourceAccessPermissions.ContainsKey(urn))
                    return permission.DataSourceAccessPermissions[urn].AccessLevel;
            }
            return 0;
        }

        public void NotifyAdministrators(Dictionary<string, AccessLevel> permissions)
        {
            var collection = _mongoDatabase.GetCollection<Permission>(PermissionsCollectionName);
            var administartorPermissions =
                collection.FindAs<Permission>(Query<Permission>.EQ(p => p.UserRole, UserRole.Administrator));

            foreach (var administartorPermission in administartorPermissions)
            {
                if (NotifyPermissionChanged != null)
                    NotifyPermissionChanged(this, new PermissionEventArgs(administartorPermission.UserId, permissions));
            }
        }

        public event EventHandler<PermissionEventArgs> NotifyPermissionChanged;
    }
}
