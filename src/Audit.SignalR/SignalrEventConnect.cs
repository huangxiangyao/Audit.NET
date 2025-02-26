﻿using System.Collections.Generic;
using Microsoft.AspNet.SignalR.Hubs;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Audit.SignalR
{
    /// <summary>
    /// Represents a Connect SignalR event
    /// </summary>
    public class SignalrEventConnect : SignalrEventBase
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public override SignalrEventType EventType => SignalrEventType.Connect;
        public string ConnectionId { get; set; }
        public IDictionary<string, string> Headers { get; set; }
        public IDictionary<string, string> QueryString { get; set; }
        public string LocalPath { get; set; }
        public string IdentityName { get; set; }
        [JsonIgnore]
        public IHub HubReference { get; set; }
    }
}