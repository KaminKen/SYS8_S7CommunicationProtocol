using System;
using System.Diagnostics;

namespace SYS8.Core.StringManipulation
{
    public class StringAddressToAbsoluteAddress
    {
        public (ushort dbNumber, int byteOffset, int bitIndex) ParseStringAddress(string address)
        {
            // DB1.DBX0.0

            Debug.WriteLine($"Parsing address: {address}");

            string[] parts = address.Split('.');

            if (parts.Length != 3)
            {
                throw new FormatException("Invalid address format. Expected format: DB{number}.DBX{byteOffset}.{bitIndex}");
            }

            if (!parts[0].StartsWith("DB"))
            {
                throw new FormatException("Address must start with DB.");
            }

            if (!parts[1].StartsWith("DBX"))
            {
                throw new FormatException("Second part must be DBX{byteOffset}.");
            }

            ushort dbNumber = ushort.Parse(parts[0].Substring(2)); // Take the part after "DB" and parse it as DB number
            int byteOffset = int.Parse(parts[1].Substring(3)); // Take the part after "DBX" and parse it as byte offset
            int bitIndex = int.Parse(parts[2]);

            if (bitIndex < 0 || bitIndex > 7)
            {
                throw new FormatException("Bit index must be between 0 and 7.");
            }

            Debug.WriteLine($"dbNumber={dbNumber}, byteOffset={byteOffset}, bitIndex={bitIndex}");

            return (dbNumber, byteOffset, bitIndex);
        }


        public string ConvertToAbsoluteAddress(string address)
        {
            var (dbNumber, byteOffset, bitIndex) = ParseStringAddress(address);
            return ConvertToAbsoluteAddress(dbNumber, byteOffset, bitIndex);
        }



        public string ConvertToAbsoluteAddress(ushort dbNumber, int byteOffset, int bitIndex)
        {
            int absoluteByteOffset = (dbNumber - 1) * 256 + byteOffset;
            // Return the absolute address in the format "DB{dbNumber}.DBX{absoluteByteOffset}.{bitIndex}"
            return $"DB{dbNumber}.DBX{absoluteByteOffset}.{bitIndex}";
        }
    }
}