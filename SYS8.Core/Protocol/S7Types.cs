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

        // 16-bit: WORD/UINT and INT are the same 2 bytes on the wire; S7ANY uses one transport code for this size class.
        //public const byte Word = 0x04;
        public const byte Word = 0x05; // WORD / UINT (same encoding as INT for DB bit-addressed access) - obtained from reverse engineering and trial and error
        public const byte Int = 0x05;  // INT

        // 32-bit: DWORD/UDINT and DINT are the same 4 bytes on the wire for raw DB access.
        //public const byte DWord = 0x06;
        public const byte DWord = 0x07; // DWORD / UDINT (same encoding as DINT for DB access) - obtained from reverse engineering and trial and error
        public const byte DInt = 0x07;  // DINT

        // Floating point family
        // Used for REAL (4 bytes) and LREAL (8 bytes),
        // the element length (4 vs 8) tells the PLC which you want.
        public const byte Real = 0x08;
        public const byte LReal = 0x09;
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