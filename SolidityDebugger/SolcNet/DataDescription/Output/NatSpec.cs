using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace SolcNet.DataDescription.Output
{
    public class Doc
    {
        [JsonProperty("methods")]
        public Dictionary<string /*function signature*/, MethodDoc> Methods { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("notice")]
        public string Notice { get; set; }

        [JsonProperty("details")]
        public string Details { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }
        
        [JsonProperty("events")]
        public Dictionary<string /*function signature*/, MethodDoc> Events { get; set; }
        
        [JsonProperty("stateVariables")]
        [JsonIgnore]
        public string StateVariables { get; set; }
        public static implicit operator Doc(string json) => JsonConvert.DeserializeObject<Doc>(json);
    }

    public class MethodDoc
    {
        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("details")]
        public string Details { get; set; }

        [JsonProperty("notice")]
        public string Notice { get; set; }

        [JsonProperty("params")]
        public Dictionary<string /*param name*/, string /*description*/> Params { get; set; }

        [JsonProperty("return")]
        public string Return { get; set; }
        
        [JsonProperty("returns")]
        public Dictionary<string /*param name*/, string /*description*/> Returns { get; set; }
    }

}
