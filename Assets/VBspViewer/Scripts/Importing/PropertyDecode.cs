using System;

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

    }
}
