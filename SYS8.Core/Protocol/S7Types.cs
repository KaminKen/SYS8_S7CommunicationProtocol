namespace SYS8.Core.Protocol;

internal static class S7Types
{
    internal static class ItemTransport
    {
        public const byte Bit = 0x01;
        public const byte Byte = 0x02;
        public const byte Char = 0x03;
        public const byte Word = 0x04;
        public const byte Int = 0x05;
        public const byte DWord = 0x06;
        public const byte DInt = 0x07;
        public const byte Real = 0x08;
    }

    internal static class DataTransport
    {
        public const byte Bit = 0x03; // len in bits
        public const byte ByteWordDword = 0x04; // len in bits
        public const byte Integer = 0x05;
        public const byte Real = 0x07;
        public const byte OctetString = 0x09;
    }
}
