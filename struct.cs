public static class Struct
{
    /// <summary>
    /// 根据结构体成员标注的特性对结构体值的字节数组进行字节翻转
    /// </summary>
    private static byte[] RespectEndianness(byte[] bytes, Type type)
    {
        FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (FieldInfo field in fields)
        {
            if (field.IsDefined(typeof(EndianAttribute), false) && Marshal.SizeOf(field.FieldType) > 1)
            {
                EndianAttribute endianattr = (EndianAttribute)field.GetCustomAttributes(typeof(EndianAttribute), false)[0];
                if ((endianattr.Endian == Endianness.BigEndian && BitConverter.IsLittleEndian) || (endianattr.Endian == Endianness.LittleEndian && !BitConverter.IsLittleEndian))
                {
                    int mag = 1;
                    if (field.IsDefined(typeof(MarshalAsAttribute), false) && field.GetType().IsArray)
                    {
                        MarshalAsAttribute marshalattr = (MarshalAsAttribute)field.GetCustomAttributes(typeof(MarshalAsAttribute), false)[0];
                        mag = marshalattr.SizeConst > 1 ? marshalattr.SizeConst : 1;
                    }
                    for (int i = 0; i < mag; i++)
                        Array.Reverse(bytes, Marshal.OffsetOf(type, field.Name).ToInt32() + (i * Marshal.SizeOf(field.FieldType)), Marshal.SizeOf(field.FieldType));
                }
            }
        }
        return bytes;
    }

    /// <summary>
    /// 将byte[]解析为结构体T
    /// </summary>
    public static T UnPack<T>(byte[] bytes) where T : struct
    {
        Type type = typeof(T);
        GCHandle handle = GCHandle.Alloc(RespectEndianness(bytes, type), GCHandleType.Pinned);
        try
        {
            return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), type);
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>
    /// 将结构体T打包为byte[]
    /// </summary>
    public static byte[] Pack<T>(T structure) where T : struct
    {
        Type type = typeof(T);
        byte[] bytes = new byte[Marshal.SizeOf(structure)];
        GCHandle handler = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(structure, handler.AddrOfPinnedObject(), true);
        }
        finally
        {
            handler.Free();
        }
        RespectEndianness(bytes, type);
        return bytes;
    }
}

public class EndianAttribute : Attribute
{
    public Endianness Endian { get; private set; }
    public int Size { get; private set; }
    public EndianAttribute(Endianness endian)
    {
        Endian = endian;
    }
    public EndianAttribute(Endianness endian, int size)
    {
        Endian = endian;
        Size = size;
    }
}

public enum Endianness
{
    BigEndian,
    LittleEndian
}
