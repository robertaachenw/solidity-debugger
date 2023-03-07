using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Meadow.EVM.Data_Types;
using Meadow.EVM.Data_Types.Addressing;
using Meadow.EVM.EVM.Execution;
using Meadow.Plugin;

namespace Meadow.EVM.EVM.Instructions.Environment
{
    public class InstructionAddress : InstructionBase
    {
        //#region Constructors
        /// <summary>
        /// Our default constructor, reads the opcode/operand information from the provided stream.
        /// </summary>
        public InstructionAddress(MeadowEVM evm) : base(evm) { }
        //#endregion

        //#region Functions
        public override void Execute()
        {
            // cheat implementation
            if (EVM.State.SpoofAddress.ContainsKey(Message.To.ToBigInteger()))
            {
                var spoofValue = new Address(EVM.State.SpoofAddress[Message.To.ToBigInteger()]);
                Stack.Push(spoofValue);
                return;
            }
            
            // Push the recipient address of the message to the stack.
            Stack.Push(Message.To);
        }
        //#endregion
    }
}
