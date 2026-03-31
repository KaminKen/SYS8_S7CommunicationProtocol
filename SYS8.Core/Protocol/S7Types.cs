namespace SYS8.Core.Protocol;

internal static class S7Types
{
    /// <summary>
    /// Item transport size codes used in S7Any parameter[5]
    /// to describe the S7 data type you are requesting.
    /// </summary>
    internal static class ItemTransport
    {
        // Single bit
        public const byte Bit = 0x01;

        // 8-bit byte/char
        public const byte Byte = 0x02;
        public const byte Char = 0x03;

        // 16-bit integer family
        public const byte Word = 0x04; // 16-bit unsigned (WORD / UINT)
        public const byte Int = 0x05; // 16-bit signed   (INT)

        // 32-bit integer family
        public const byte DWord = 0x06; // 32-bit unsigned (DWORD / UDINT)
        public const byte DInt = 0x07; // 32-bit signed   (DINT)

        // Floating point family
        // Used for REAL (4 bytes) and LREAL (8 bytes),
        // the element length (4 vs 8) tells the PLC which you want.
        public const byte Real = 0x08;
    }

    /// <summary>
    /// Data transport size codes used in the data header (response),
    /// respPayload[dataHeaderStartIndex + 1]. These describe how to
    /// interpret the payload and the following length field.
    /// </summary>
    internal static class DataTransport
    {
        // BOOL: length is in bits
        public const byte Bit = 0x03;

        // BYTE/WORD/DWORD: length is in bits
        public const byte ByteWordDword = 0x04;

        // Integer family (INT/DINT/LINT/UINT/UDINT/ULINT): length in bits
        public const byte Integer = 0x05;

        // Floating point family (REAL / LREAL): length in bytes
        public const byte Real = 0x07;

        // Raw bytes / STRING / OCTET STRING: length in bytes
        public const byte OctetString = 0x09;
    }
}