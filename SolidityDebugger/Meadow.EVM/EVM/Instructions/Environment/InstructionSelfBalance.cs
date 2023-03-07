using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Meadow.EVM.Data_Types;
using Meadow.EVM.Data_Types.Addressing;
using Meadow.EVM.EVM.Execution;

namespace Meadow.EVM.EVM.Instructions.Environment
{
    public class InstructionSelfBalance : InstructionBase
    {
        public InstructionSelfBalance(MeadowEVM evm) : base(evm) { }

        public override void Execute()
        {
            Stack.Push(EVM.State.GetBalance(Message.To));
        }
    }
}
