﻿using System.Linq;
using FalconSoft.Data.Management.Common.Facades;
using FalconSoft.Data.Management.Common.Security;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace FalconSoft.Data.Management.Server.SignalR.Hubs
{
    [HubName("IPermissionSecurityHub")]
    public class PermissionSecurityHub : Hub
    {
        private readonly IPermissionSecurityFacade _permissionSecurityPersistance;
        public PermissionSecurityHub(IPermissionSecurityFacade permissionSecurityPersistance)
        {
            _permissionSecurityPersistance = permissionSecurityPersistance;
        }

        public Permission[] GetUserPermissions(string userToken)
        {
            var data = _permissionSecurityPersistance.GetUserPermissions(userToken);
            if (data!=null)
            return data.ToArray();
            else
            {
                return new Permission[0];
            }
        }

        public void SaveUserPermissions(string connectionId, Permission[] permissions, string targetUserToken, string grantedByUserToken)
        {
            _permissionSecurityPersistance.SaveUserPermissions(permissions, targetUserToken, grantedByUserToken, message => Clients.Client(connectionId).OnMessageAction(message));
        }
    }
}