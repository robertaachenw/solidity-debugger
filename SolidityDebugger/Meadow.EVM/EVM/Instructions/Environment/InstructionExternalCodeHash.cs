using Meadow.Core.Cryptography;
using Meadow.Core.Utils;
using Meadow.EVM.Data_Types.Addressing;

namespace Meadow.EVM.EVM.Instructions.Environment
{
    public class InstructionExternalCodeHash : InstructionBase
    {
        public InstructionExternalCodeHash(MeadowEVM evm) : base(evm) { }

        public override void Execute()
        {
            Address externalCodeAddress = Stack.Pop();

            var codeSegment = EVM.State.GetCodeSegment(externalCodeAddress);
            if (codeSegment == null || codeSegment.Length == 0)
            {
                Stack.Push(0);
                return;
            }
            
            Stack.Push(BigIntegerConverter.GetBigInteger(KeccakHash.ComputeHashBytes(codeSegment)));
        }
    }
}
