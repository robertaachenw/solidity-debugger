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
    public class InstructionCaller : InstructionBase
    {
        //#region Constructors
        /// <summary>
        /// Our default constructor, reads the opcode/operand information from the provided stream.
        /// </summary>
        public InstructionCaller(MeadowEVM evm) : base(evm) { }
        //#endregion

        //#region Functions
        public override void Execute()
        {
            if (EVM.State.SpoofCaller.ContainsKey(Message.Sender.ToBigInteger()))
            {
                var spoofTo = new Address(EVM.State.SpoofCaller[Message.Sender.ToBigInteger()]);
                Stack.Push(spoofTo);
                return;
            }
            
            // Push the sender address of the message to the stack.
            Stack.Push(Message.Sender);
        }
        //#endregion
    }
}
