﻿using System.Collections.Generic;
using System.Linq;
using FalconSoft.ReactiveWorksheets.Common.Security;
using FalconSoft.ReactiveWorksheets.Core;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace FalconSoft.ReactiveWorksheets.Persistence.Security
{
    public class SecurityPersistence : ISecurityPersistence
    {

        private readonly string _connectionString;
        private const string UsersCollectionName = "Users";
        private MongoDatabase _mongoDatabase;

        public SecurityPersistence(string connectionString)
        {
            _connectionString = connectionString;
        }

        private void ConnectToDb()
        {
            if (_mongoDatabase == null || _mongoDatabase.Server.State != MongoServerState.Connected)
            {
                _mongoDatabase = MongoDatabase.Create(_connectionString);
            }
        }

        public List<User> GetUsers()
        {
            ConnectToDb();
            return _mongoDatabase.GetCollection<User>(UsersCollectionName).FindAll().ToList();
        }

        public void SaveNewUser(User user)
        {
            ConnectToDb();
            user.Id = ObjectId.GenerateNewId().ToString();
            _mongoDatabase.GetCollection<User>(UsersCollectionName).Insert(user);
        }

        public void UpdateUser(User user)
        {
            ConnectToDb();
            _mongoDatabase.GetCollection<User>(UsersCollectionName).Update(Query<User>.EQ(u => u.Id, user.Id), Update<User>.Set(u => u.LoginName, user.LoginName)
                                                                                                           .Set(u => u.FirstName, user.FirstName)
                                                                                                           .Set(u => u.LastName, user.LastName));
        }

        public void RemoveUser(User user)
        {
            ConnectToDb();
            _mongoDatabase.GetCollection<User>(UsersCollectionName).Remove(Query.EQ("Id", new BsonString(user.Id)));
        }
    }
}
