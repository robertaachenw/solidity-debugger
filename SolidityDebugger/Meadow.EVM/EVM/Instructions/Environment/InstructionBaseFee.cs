using System.Numerics;

namespace Meadow.EVM.EVM.Instructions.Environment
{
    public class InstructionBaseFee : InstructionBase
    {
        public InstructionBaseFee(MeadowEVM evm) : base(evm) { }

        public override void Execute()
        {
            Stack.Push(BigInteger.Pow(10, 11));
        }
    }
}
