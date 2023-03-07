using Meadow.Core;

namespace Meadow.Plugin
{
    public delegate void CheatCodeHandler(
        string name,
        SolidityType[] argTypes,
        object[] argValues,
        object context,
        object evmMessage);

    public interface ICheatCodeRegistry
    {
        void Register(string name, SolidityType[] argTypes, CheatCodeHandler handler, object context);
    }
}