using System;
using Newtonsoft.Json;

namespace SolcNet.DataDescription.Output
{
    public class FunctionDebugData
    {
        /// <summary>
        /// Byte offset into the bytecode where the function starts (optional) 
        /// </summary>
        [JsonProperty("entryPoint", Required = Required.AllowNull)]
        public int? EntryPoint { get; set; }
        
        /// <summary>
        /// AST ID of the function definition or null for compiler-internal functions (optional) 
        /// </summary>
        [JsonProperty("id", Required = Required.AllowNull)]
        public int? Id { get; set; }
        
        /// <summary>
        /// Number of EVM stack slots for the function parameters (optional) 
        /// </summary>
        [JsonProperty("parameterSlots", Required = Required.AllowNull)]
        public int? ParameterSlots { get; set; }
        
        /// <summary>
        /// Number of EVM stack slots for the return values (optional) 
        /// </summary>
        [JsonProperty("returnSlots", Required = Required.AllowNull)]
        public int? ReturnSlots { get; set; }
    }
}