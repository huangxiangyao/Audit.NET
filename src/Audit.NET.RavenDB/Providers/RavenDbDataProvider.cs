﻿using Audit.Core;
using Audit.NET.RavenDB.ConfigurationApi;
using Raven.Client.Documents;
using Raven.Client.Json.Serialization.NewtonsoftJson;
using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
#if IS_TEXT_JSON
using Audit.JsonNewtonsoftAdapter;
#endif

namespace Audit.NET.RavenDB.Providers
{
    /// <summary>
    /// Data provider for persisting Audit Events as documents into a Raven DB 
    /// </summary>
    [CLSCompliant(false)]
    public class RavenDbDataProvider : AuditDataProvider
    {
        private IDocumentStore _documentStore;
        private readonly Func<AuditEvent, string> _databaseNameFunc;

#if IS_TEXT_JSON
        /// <summary>
        /// Json default settings, used only when the current Audit.NET json adapter is not the Newtonsoft adapter.
        /// </summary>
        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };
#endif

        /// <summary>
        /// The Raven Document Store
        /// </summary>
        public IDocumentStore DocumentStore
        {
            get => _documentStore;
            set => _documentStore = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RavenDbDataProvider"/> class using a custom Document Store instance.
        /// </summary>
        /// <param name="documentStore">The document store.</param>
        /// <param name="databaseFunc">The function to obtain the database name from the audit event.</param>
        public RavenDbDataProvider(IDocumentStore documentStore, Func<AuditEvent, string> databaseFunc = null)
        {
            _databaseNameFunc = databaseFunc;
            _documentStore = documentStore;
            _documentStore.Initialize();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RavenDbDataProvider"/> class using the given configuration.
        /// </summary>
        /// <param name="config">The RavenDB configuration fluent API.</param>
        public RavenDbDataProvider(Action<IRavenDbProviderConfigurator> config)
        {
            var ravenConfig = new RavenDbProviderConfigurator();
            config.Invoke(ravenConfig);

            _databaseNameFunc = ravenConfig._storeConfig._databaseFunc;

            if (ravenConfig._documentStore == null)
            {
                _documentStore = new DocumentStore()
                {
                    Certificate = ravenConfig._storeConfig._certificate, 
                    Urls = ravenConfig._storeConfig._urls, 
                    Database = ravenConfig._storeConfig._databaseDefault
                };

#if IS_TEXT_JSON
                (_documentStore.Conventions.Serialization as NewtonsoftJsonSerializationConventions)!
                    .JsonContractResolver = new AuditContractResolver();
#else
                (_documentStore.Conventions.Serialization as NewtonsoftJsonSerializationConventions)!
                    .JsonContractResolver = new DefaultContractResolver();
#endif
            }
            else
            {
                _documentStore = ravenConfig._documentStore;
            }

            _documentStore.Initialize();
        }

#if IS_TEXT_JSON
        public override object Serialize<T>(T value)
        {
            if (value == null)
            {
                return null;
            }
            if (value is string)
            {
                return value;
            }
            if (Configuration.JsonAdapter is Audit.Core.JsonNewtonsoftAdapter adapter)
            {
                // The adapter is Newtonsoft, use the adapter
                return adapter.Deserialize<T>(adapter.Serialize(value));
            }
            // Default to use Newtonsoft directly
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value, JsonSerializerSettings), JsonSerializerSettings);
        }
#endif

        public override object InsertEvent(AuditEvent auditEvent)
        {
            using (var session = _documentStore.OpenSession(GetDatabaseName(auditEvent)))
            {
                session.Store(auditEvent);
                session.SaveChanges();

                return session.Advanced.GetDocumentId(auditEvent);
            }
        }

        /// <summary>
        /// Insert an event to the data source returning the event id generated
        /// </summary>
        /// <param name="auditEvent">The audit event being inserted.</param>
        /// <returns></returns>
        public override async Task<object> InsertEventAsync(AuditEvent auditEvent)
        {
            using (var session = _documentStore.OpenAsyncSession(GetDatabaseName(auditEvent)))
            {
                await session.StoreAsync(auditEvent);
                await session.SaveChangesAsync();

                return session.Advanced.GetDocumentId(auditEvent);
            }
        }

        /// <summary>
        /// Retrieves a saved audit event from its id. Override this method to provide a way to access the audit events by id.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="eventId">The event id being retrieved.</param>
        /// <returns></returns>
        public override T GetEvent<T>(object eventId)
        {
            using (var session = _documentStore.OpenSession(GetDatabaseName()))
            {
                var auditEvent = session.Load<T>(eventId.ToString());
                return auditEvent;
            }
        }

        /// <summary>
        /// Asychronously retrieves a saved audit event from its id. Override this method
        /// to provide a way to access the audit events by id.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="eventId">The event id being retrieved.</param>
        /// <returns></returns>
        public override async Task<T> GetEventAsync<T>(object eventId)
        {
            using (var session = _documentStore.OpenAsyncSession(GetDatabaseName()))
            {
                var auditEvent = await session.LoadAsync<T>(eventId.ToString());
                return auditEvent;
            }
        }

        /// <summary>
        /// Saves the specified audit event. Triggered when the scope is saved. Override
        /// this method to replace the specified audit event on the data source.
        /// </summary>
        /// <param name="eventId">The event id being replaced.</param>
        /// <param name="auditEvent">The audit event.</param>
        public override void ReplaceEvent(object eventId, AuditEvent auditEvent)
        {
            using (var session = _documentStore.OpenSession(GetDatabaseName(auditEvent)))
            {
                session.Store(auditEvent, eventId.ToString());
                session.SaveChanges();
            }
        }

        /// <summary>
        /// Saves the specified audit event. Triggered when the scope is saved. Override
        /// this method to replace the specified audit event on the data source.
        /// </summary>
        /// <param name="eventId">The event id being replaced.</param>
        /// <param name="auditEvent">The audit event.</param>
        /// <returns></returns>
        public override async Task ReplaceEventAsync(object eventId, AuditEvent auditEvent)
        {
            using (var session = _documentStore.OpenAsyncSession(GetDatabaseName(auditEvent)))
            {
                await session.StoreAsync(auditEvent, eventId.ToString());
                await session.SaveChangesAsync();
            }
        }

        internal string GetDatabaseName(AuditEvent auditEvent = null)
        {
            return _databaseNameFunc?.Invoke(auditEvent);
        }
    }
}
