using System;
using System.Net.Security;
using System.Runtime.Remoting.Channels;
using System.Text;
using UnityEngine;
using VBspViewer.Importing.Dem.Generated;

namespace VBspViewer.Importing
{
    enum SendPropType
    {
        Int = 0,
        Float,
        Vector,
        VectorXY, // Only encodes the XY of a vector, ignores Z
        String,
        Array,  // An array of the base types (can't be of datatables).
        DataTable,
        Int64,
        NUMSendPropTypes
    }

    [Flags]
    public enum SendPropFlag
    {
        UNSIGNED = (1 << 0),
        COORD = (1 << 1),
        NOSCALE = (1 << 2),
        ROUNDDOWN = (1 << 3),
        ROUNDUP = (1 << 4),
        NORMAL = (1 << 5),
        EXCLUDE = (1 << 6),
        XYZE = (1 << 7),
        INSIDEARRAY = (1 << 8),
        PROXY_ALWAYS_YES = (1 << 9),
        IS_A_VECTOR_ELEM = (1 << 10),
        COLLAPSIBLE = (1 << 11),
        COORD_MP = (1 << 12),
        COORD_MP_LOWPRECISION = (1 << 13),
        COORD_MP_INTEGRAL = (1 << 14),
        CELL_COORD = (1 << 15),
        CELL_COORD_LOWPRECISION = (1 << 16),
        CELL_COORD_INTEGRAL = (1 << 17),
        CHANGES_OFTEN = (1 << 18),
        VARINT = (1 << 19)
    }

    public static class PropertyDecode
    {
        public static int DecodeInt(BitBuffer bitBuffer, CSVCMsgSendTable.SendpropT prop)
        {
            var flags = (SendPropFlag) prop.Flags;

            if ((flags & SendPropFlag.VARINT) != 0)
            {
                if ((flags & SendPropFlag.UNSIGNED) != 0)
                {
                    return (int) bitBuffer.ReadVarInt32();
                }

                return bitBuffer.ReadSignedVarInt32();
            }

            if ((flags & SendPropFlag.UNSIGNED) != 0)
            {
                return (int) bitBuffer.ReadUBitLong(prop.NumBits);
            }

            return bitBuffer.ReadSBitLong(prop.NumBits);
        }

        private static bool DecodeSpecialFloat(BitBuffer bitBuffer, CSVCMsgSendTable.SendpropT prop, out float val)
        {
            val = default(float);

            var flags = (SendPropFlag) prop.Flags;

            if ((flags & SendPropFlag.COORD) != 0)
            {
                val = bitBuffer.ReadBitCoord();
                return true;
            }

            if ((flags & SendPropFlag.COORD_MP) != 0)
            {
                val = bitBuffer.ReadBitCoordMP(BitCoordType.None);
                return true;
            }

            if ((flags & SendPropFlag.COORD_MP_LOWPRECISION) != 0)
            {
                val = bitBuffer.ReadBitCoordMP(BitCoordType.LowPrecision);
                return true;
            }

            if ((flags & SendPropFlag.CELL_COORD_INTEGRAL) != 0)
            {
                val = bitBuffer.ReadBitCoordMP(BitCoordType.Integral);
                return true;
            }

            if ((flags & SendPropFlag.NOSCALE) != 0)
            {
                val = bitBuffer.ReadBitFloat();
                return true;
            }

            if ((flags & SendPropFlag.NORMAL) != 0)
            {
                val = bitBuffer.ReadBitNormal();
                return true;
            }

            if ((flags & SendPropFlag.CELL_COORD) != 0)
            {
                val = bitBuffer.ReadBitCellCoord(prop.NumBits, BitCoordType.None);
                return true;
            }

            if ((flags & SendPropFlag.CELL_COORD_LOWPRECISION) != 0)
            {
                val = bitBuffer.ReadBitCellCoord(prop.NumBits, BitCoordType.LowPrecision);
                return true;
            }

            if ((flags & SendPropFlag.CELL_COORD_INTEGRAL) != 0)
            {
                val = bitBuffer.ReadBitCellCoord(prop.NumBits, BitCoordType.Integral);
                return true;
            }

            return false;
        }

        public static float DecodeFloat(BitBuffer bitBuffer, CSVCMsgSendTable.SendpropT prop)
        {
            var val = 0f;
            if (DecodeSpecialFloat(bitBuffer, prop, out val)) return val;

            var interp = bitBuffer.ReadUBitLong(prop.NumBits);

            val = (float) interp/((1 << prop.NumBits) - 1);
            val = prop.LowValue + (prop.HighValue - prop.LowValue)*val;

            return val;
        }

        public static Vector3 DecodeVector(BitBuffer bitBuffer, CSVCMsgSendTable.SendpropT prop)
        {
            var vec = new Vector3
            {
                x = DecodeFloat(bitBuffer, prop),
                z = DecodeFloat(bitBuffer, prop)
            };

            if (((SendPropFlag) prop.Flags & SendPropFlag.NORMAL) == 0)
            {
                vec.y = DecodeFloat(bitBuffer, prop);
                return vec;
            }

            var sign = bitBuffer.ReadOneBit() ? -1 : 1;
            var a2b2 = vec.x*vec.x + vec.z*vec.z;

            if (a2b2 < 1f)
            {
                vec.y = Mathf.Sqrt(1f - a2b2) * sign;
            }

            return vec;
        }

        public static Vector2 DecodeVectorXY(BitBuffer bitBuffer, CSVCMsgSendTable.SendpropT prop)
        {
            return new Vector2
            {
                x = DecodeFloat(bitBuffer, prop),
                y = DecodeFloat(bitBuffer, prop)
            };
        }

        public static string DecodeString(BitBuffer bitBuffer, CSVCMsgSendTable.SendpropT prop)
        {
            const int maxStringBits = 9;
            const int maxStringLength = 1 << maxStringBits;

            var len = bitBuffer.ReadUBitLong(maxStringBits);
            var buffer = new byte[len + 1];

            if (len >= maxStringLength)
            {
                throw new Exception("Mathematics has failed us");
            }

            bitBuffer.ReadBits(buffer, (int) len << 3);

            return Encoding.UTF8.GetString(buffer);
        }

        public static long DecodeInt64(BitBuffer bitBuffer, CSVCMsgSendTable.SendpropT prop)
        {
            if (((SendPropFlag) prop.Flags & SendPropFlag.VARINT) != 0)
            {
                if (((SendPropFlag) prop.Flags & SendPropFlag.UNSIGNED) != 0)
                {
                    return (long) bitBuffer.ReadVarInt64();
                }

                return bitBuffer.ReadSignedVarInt64();
            }

            uint highInt = 0;
            uint lowInt = 0;
            var sign = 1;

            if (((SendPropFlag) prop.Flags & SendPropFlag.UNSIGNED) == 0)
            {
                sign = bitBuffer.ReadOneBit() ? -1 : 1;
                lowInt = bitBuffer.ReadUBitLong(32);
                highInt = bitBuffer.ReadUBitLong(prop.NumBits - 32 - 1);
            }
            else
            {
                lowInt = bitBuffer.ReadUBitLong(32);
                highInt = bitBuffer.ReadUBitLong(prop.NumBits - 32);
            }

            long temp = lowInt | highInt << 32;
            return temp*sign;
        }
    }
}
