using Meadow.EVM.Data_Types;
using Meadow.EVM.EVM.Execution;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace Meadow.EVM.EVM.Instructions.Block_Information
{
    public class InstructionChainId : InstructionBase
    {
        public InstructionChainId(MeadowEVM evm) : base(evm) { }

        public override void Execute()
        {
            if (EVM.State.SpoofChainId != BigInteger.Zero)
            {
                Stack.Push(EVM.State.SpoofChainId);
                return;
            }
            
            Stack.Push((int)EVM.ChainID);
        }
    }
}
