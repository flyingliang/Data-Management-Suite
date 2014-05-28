﻿using System;
using System.Linq;
using FalconSoft.Data.Server.Common;
using FalconSoft.Data.Server.Common.Facade;
using FalconSoft.Data.Server.Components;
using FalconSoft.Data.Server.Components.CommandsAggregator;
using FalconSoft.Data.Server.Components.Facades;
using FalconSoft.Data.Server.Components.Infrastructure;
using FalconSoft.Data.Server.Components.ReactiveEngine;
using FalconSoft.Data.Server.DefaultMongoDbSource;
using FalconSoft.Data.Server.Persistence;
using FalconSoft.Data.Server.Persistence.LiveData;
using FalconSoft.Data.Server.Persistence.MetaData;
using FalconSoft.Data.Server.Persistence.SearchIndexes;
using FalconSoft.Data.Server.Persistence.Security;
using FalconSoft.Data.Server.Persistence.TemporalData;
using FalconSoft.Data.Server.Common.Metadata;

namespace FalconSoft.Data.Server
{
    public static class ServerApp
    {
        private static string _metaDataPersistenceConnectionString;
        private static string _persistenceDataConnectionString;
        private static string _mongoDataConnectionString;

        internal static void SetConfiguration(string metaDataPersistenceConnectionString, 
            string persistenceDataConnectionString,
            string mongoDataConnectionString)
        {
            _metaDataPersistenceConnectionString = metaDataPersistenceConnectionString;
            _persistenceDataConnectionString = persistenceDataConnectionString;
            _mongoDataConnectionString = mongoDataConnectionString;
        }

        private static ICommandsAggregator _commandAggregator;

        private static IProvidersRegistry _providersRegistry;

        private static IDataProvidersCatalog[] _dataProvidersCatalogs;

        private static IReactiveEngine _reactiveEngine;

        private static IMetaDataAdminFacade _metaDataFacade;

        private static IReactiveDataQueryFacade _dataQueryFacade;

        private static ITemporalDataQueryFacade _temporalQueryFacade;

        private static ICommandFacade _commandFacade;

        private static ISearchFacade _searchFacade;

        private static ISecurityFacade _securityFacade;

        private static Func<string, ILiveDataPersistence> _liveDataPersistenceFactory;

        private static IWorksheetPersistence _worksheetsPersistence;

        private static Func<DataSourceInfo, ITemporalDataPersistense> _temporalDataPersistenseFactory;

        private static ISearchIndexPersistence _searchIndexPersistence;

        private static ISecurityPersistence _securityPersistence;

        private static IMetaDataPersistence _metaDataPersistence;

        private static IMetaDataProvider _metaDataProvider;

        private static IMessageBus _messageBus;

        private static ILogger _logger;

        public static IMessageBus MessageBus
        {
            get
            {
                return _messageBus ?? (_messageBus = new ReactiveBus());
            }
        }

        public static IMetaDataAdminFacade MetaDataFacade
        {
            get
            {
                return _metaDataFacade ?? (_metaDataFacade = new MetaDataFacade(ProvidersRegistry, WorksheetsPersistence, MetaDataPersistence));
            }
        }

        public static IReactiveDataQueryFacade ReactiveDataQueryFacade
        {
            get
            {
                return _dataQueryFacade ?? (_dataQueryFacade = new ReactiveDataFacade(MessageBus, LiveDataPersistenceFactory,ProvidersRegistry,ReactiveEngine));
            }
        }

        public static ITemporalDataQueryFacade TemporalQueryFacade
        {
            get
            {
                return _temporalQueryFacade ??
                       (_temporalQueryFacade = new TemporalDataQueryFacade(MessageBus, TemporalDataPersistenseFactory,ProvidersRegistry));
            }
        }

        public static ICommandFacade CommandFacade
        {
            get
            {
                return _commandFacade ?? (_commandFacade = new CommandFacade(CommandAggregator));
            }
        }
        
        public static ISearchFacade SearchFacade
        {
            get
            {
                return _searchFacade ?? (_searchFacade = new SearchFacade(MessageBus, SearchIndexPersistence, MetaDataFacade, LiveDataPersistenceFactory));
            }
        }

        public static ISecurityFacade SecurityFacade
        {
            get
            {
                return _securityFacade ?? (_securityFacade = new SecurityFacade(SecurityPersistence));
            }
        }

        public static IDataProvidersCatalog[] DataProvidersCatalogs
        {
            get
            {
                if (_dataProvidersCatalogs != null) return _dataProvidersCatalogs;
                AppDomainAssemblyTypeScanner.Start(Logger);
                Logger.Info("");
                _dataProvidersCatalogs = AppDomainAssemblyTypeScanner.TypesOf(typeof (IDataProvidersCatalog)).Select(
                            x =>
                            {
                                Logger.Info(string.Format("-> Load {0} dll with provider Catalogs", x.FullName));
                                return (IDataProvidersCatalog) Activator.CreateInstance(x);
                            }).ToArray();
                return _dataProvidersCatalogs;
            }
        }

        public static IReactiveEngine ReactiveEngine
        {
            get
            {
                return _reactiveEngine ?? (_reactiveEngine = new ReactiveEngine(MetaDataFacade, LiveDataPersistenceFactory, ProvidersRegistry, new DenormalizedBusPublisher(MessageBus), Logger));
            }
        }

        public static IProvidersRegistry ProvidersRegistry
        {
            get
            {
                return _providersRegistry ?? (_providersRegistry = new ProvidersRegistry {DataProvidersCatalog = DataProvidersCatalogs.First(x=>x is DataProvidersCatalog)});
            }
        }

        public static ICommandsAggregator CommandAggregator
        {
            get
            {
                return _commandAggregator ??
                       (_commandAggregator = new CommandAggregator(ProvidersRegistry, LiveDataPersistenceFactory,
                                                                   TemporalDataPersistenseFactory,
                                                                   DataProvidersCatalogs, ReactiveEngine, MetaDataPersistence, DefaultMetaDataProvider, Logger));
            }
        }

        public static IWorksheetPersistence WorksheetsPersistence
        {
            get
            {
                return _worksheetsPersistence ??
                       (_worksheetsPersistence = new WorksheetPersistence(_metaDataPersistenceConnectionString));
            }
        }

        public static Func<string, ILiveDataPersistence> LiveDataPersistenceFactory
        {
            get
            {
                // TODO : this has to move to separate method and user should be able to specify specific connection string for each dataprovider or use default one 
                return _liveDataPersistenceFactory ?? (_liveDataPersistenceFactory = s => new LiveDataPersistence(_persistenceDataConnectionString, s.GetName()));
            }
        }

        public static Func<DataSourceInfo, ITemporalDataPersistense> TemporalDataPersistenseFactory
        {
            get
            {
                return _temporalDataPersistenseFactory ?? (_temporalDataPersistenseFactory = s =>
                {
                    switch (s.HistoryStorageType)
                    {
                       case HistoryStorageType.Buffer:
                        {
                            return
                                new TemporalDataPersistenceBuffer(_persistenceDataConnectionString, s, "0", 
                                    string.IsNullOrEmpty(s.HistoryStorageTypeParam) ? 100 : int.Parse(s.HistoryStorageTypeParam));
                        }
                       case HistoryStorageType.Event:
                        {
                            return new TemporalDataPersistence(_persistenceDataConnectionString, s, "0");
                        }
                       case HistoryStorageType.Time:
                        {
                            //TODO will be soon 
                            return null;
                        }
                    }
                    //DEFAULT RETURN
                    return new TemporalDataPersistenceBuffer(_persistenceDataConnectionString, s, "0", 100);
                });
                                                                                         
            }
        }

        public static ISearchIndexPersistence SearchIndexPersistence
        {
            get
            {
                return _searchIndexPersistence ??
                       (_searchIndexPersistence = new SearchIndexPersistence(_persistenceDataConnectionString, _metaDataPersistenceConnectionString));
            }
        }

        public static ISecurityPersistence SecurityPersistence
        {
            get
            {
                return _securityPersistence ??
                       (_securityPersistence = new SecurityPersistence(_persistenceDataConnectionString));
            }
        }

        public static IMetaDataPersistence MetaDataPersistence
        {
            get
            {
                return _metaDataPersistence ??
                       (_metaDataPersistence = new MetaDataPersistence(_metaDataPersistenceConnectionString));
            }
        }

        public static IMetaDataProvider DefaultMetaDataProvider
        {
            get
            {
                return _metaDataProvider ??
                       (_metaDataProvider = new MetaDataProvider(_mongoDataConnectionString));
            }
        }

        public static ILogger Logger
        {
            get
            {
                return _logger ?? (_logger = new Logger());
            }
        }
    }
}
