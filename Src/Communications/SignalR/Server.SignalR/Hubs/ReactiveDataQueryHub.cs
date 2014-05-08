﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mime;
using System.Reactive.Linq;
using System.Runtime.Remoting.Contexts;
using System.Threading;
using System.Threading.Tasks;
using FalconSoft.ReactiveWorksheets.Common;
using FalconSoft.ReactiveWorksheets.Common.Facade;
using FalconSoft.ReactiveWorksheets.Common.Metadata;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace FalconSoft.ReactiveWorksheets.Server.SignalR.Hubs
{
    [HubName("IReactiveDataQueryFacade")]
    public class ReactiveDataQueryHub : Hub
    {
        private readonly IReactiveDataQueryFacade _reactiveDataQueryFacade;
        private readonly ILogger _logger;
        private readonly Dictionary<string, IDisposable> _getDataChangesDisposables;
        private readonly Dictionary<string, string> _dataSourcePathDictionary;

        public override Task OnConnected()
        {
            Trace.WriteLine("   Connected : " + Context.ConnectionId);
            _logger.InfoFormat("Time {0} | Connected: ConnectionId {1}, User {2}", DateTime.Now, Context.ConnectionId,
                Context.User != null ? Context.User.Identity.Name : null);
            return base.OnConnected();
        }

        public override Task OnDisconnected()
        {
            Trace.WriteLine("   Disconnected : " + Context.ConnectionId);
            if (_getDataChangesDisposables.ContainsKey(Context.ConnectionId))
            {
                Groups.Remove(Context.ConnectionId, _dataSourcePathDictionary[Context.ConnectionId]);
                _dataSourcePathDictionary.Remove(Context.ConnectionId);
                _getDataChangesDisposables[Context.ConnectionId].Dispose();
                _getDataChangesDisposables.Remove(Context.ConnectionId);
                _logger.Info("remove subscribe for " + Context.ConnectionId);
            }
            _logger.InfoFormat("Time {0} | Disconnected: ConnectionId {1}, User {2}", DateTime.Now, Context.ConnectionId,
                Context.User != null ? Context.User.Identity.Name : null);
            return base.OnDisconnected();
        }

        public ReactiveDataQueryHub(IReactiveDataQueryFacade reactiveDataQueryFacade, ILogger logger)
        {
            _getDataChangesDisposables = new Dictionary<string, IDisposable>();
            _dataSourcePathDictionary = new Dictionary<string, string>();
            _reactiveDataQueryFacade = reactiveDataQueryFacade;
            _logger = logger;
            _getDataLock= new object();
        }

        private static Object _getDataLock;

        public void GetAggregatedData(string dataSourcePath, AggregatedWorksheetInfo aggregatedWorksheet,
            FilterRule[] filterRules = null)
        {
           
            try
            {
                var data = _reactiveDataQueryFacade.GetAggregatedData(dataSourcePath, aggregatedWorksheet, filterRules);

                foreach (var d in data)
                {
                    Clients.Caller.GetAggregatedDataOnNext(d);
                }

                Clients.Caller.GetAggregatedDataOnComplete();
            }
            catch (Exception ex)
            {
                Clients.Caller.GetAggregatedDataOnError(ex);
                throw ex;
            }
        }

        public void GetGenericData(string dataSourcePath, Type type, FilterRule[] filterRules = null)
        {
            
            try
            {
                var mi = typeof (IReactiveDataQueryFacade).GetMethod("GetData");
                var miConstructed = mi.MakeGenericMethod(type);
                var result =
                    miConstructed.Invoke(_reactiveDataQueryFacade,
                        new object[] {dataSourcePath, filterRules.Any() ? filterRules : null}) as IEnumerable;

                foreach (var obj in result)
                {
                    Clients.Caller.GetGenericDataOnNext(obj);
                }

                Clients.Caller.GetGenericDataOnComplete();
            }
            catch (Exception ex)
            {
                Clients.Caller.GetGenericDataOnError(ex);
                throw ex;
            }

        }

        public void GetData(string dataSourcePath, FilterRule[] filterRules = null)
        {
            Task.Factory.StartNew(connectionId =>
            {
                Trace.WriteLine("   GetData Start connection Id : " + connectionId);
                try
                {
                    var data = _reactiveDataQueryFacade.GetData(dataSourcePath,
                        filterRules.Any() ? filterRules : null);

                    var i = 0;
                    foreach (var d in data)
                    {
                        Clients.Client(connectionId.ToString()).GetDataOnNext(d);
                        Console.Write(" \r{0}", i++);
                    }


                    Clients.Client(connectionId.ToString()).GetDataOnComplete();
                    Trace.WriteLine("   GetData Complete connection Id : " + connectionId);
                }
                catch (Exception ex)
                {
                    Clients.Client(connectionId.ToString()).GetDataOnError(ex);
                    Trace.WriteLine("   GetData Failed connection Id : " + connectionId);
                    throw ex;
                }
            }, string.Copy(Context.ConnectionId));
        }

        public void GetDataChanges(string dataSourcePath, FilterRule[] filterRules = null)
        {
            var connectionId = string.Copy(Context.ConnectionId);
            var providerString = string.Copy(dataSourcePath);

            if (!_getDataChangesDisposables.ContainsKey(connectionId))
            {
                Groups.Add(connectionId, providerString);

                var disposable = _reactiveDataQueryFacade.GetDataChanges(providerString,
                    filterRules.Any() ? filterRules : null)
                    .Subscribe(r =>
                    {
                        var dictionary = new Dictionary<string, object>(r.RecordValues);
                        var rcp = new RecordChangedParam
                        {
                            ChangeSource = r.ChangeSource,
                            ChangedPropertyNames = r.ChangedPropertyNames!=null?r.ChangedPropertyNames.ToArray(): new string[0],
                            ChangedAction = r.ChangedAction,
                            IgnoreWorksheet = r.IgnoreWorksheet,
                            OriginalRecordKey = r.OriginalRecordKey,
                            ProviderString = r.ProviderString,
                            RecordKey = r.RecordKey,
                            RecordValues = dictionary,
                            UserToken = r.UserToken
                        };
                        Clients.Group(providerString).GetDataChangesOnNext(rcp);
                    },
                        () => Groups.Remove(connectionId, providerString));
                _getDataChangesDisposables.Add(connectionId, disposable);
                _dataSourcePathDictionary.Add(connectionId, providerString);
            }
        }

        public void ResolveRecordbyForeignKey(RecordChangedParam changedRecord)
        {
            _reactiveDataQueryFacade.ResolveRecordbyForeignKey(changedRecord,
                (str, rcp) => Clients.Caller.ResolveRecordbyForeignKeySuccess(str, rcp),
                (str, ex) => Clients.Caller.ResolveRecordbyForeignKeyFailed(str, ex));
        }
    }
}
