namespace SYS8.Core.Protocol
{
    internal enum FunctionCode : byte
    {
        ReadVar = 0x04,
        WriteVar = 0x05,
        SetupCommunication = 0xF0
    }
}
