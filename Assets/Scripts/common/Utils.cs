﻿using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

public class Utils
{
    public static byte[] Serialize(object source)
    {
        try
        {
            var formatter = new BinaryFormatter();
            using (var stream = new MemoryStream())
            {
                formatter.Serialize(stream, source);
                return stream.ToArray();
            }
        }
        catch (Exception ex)
        {
            LOG.warn(ex.Message);
        }
        return null;
    }
    public static T Deserialize<T>(byte[] byteArray, int off = 0) where T : class
    {
        try
        {
            using (var memStream = new MemoryStream())
            {
                var binForm = new BinaryFormatter();
                memStream.Write(byteArray, off, byteArray.Length - off);
                memStream.Seek(0, SeekOrigin.Begin);
                var obj = (T)binForm.Deserialize(memStream);
                return obj;
            }
        }
        catch (Exception ex)
        {
            LOG.warn(ex.Message);
        }
        return null;
    }
    public static byte[] HexStringToByteArray(string hex)
    {
        return Enumerable.Range(0, hex.Length)
                         .Where(x => x % 2 == 0)
                         .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                         .ToArray();
    }
    public static byte[] Encrypt(byte[] input)
    {
        byte[] output = new byte[input.Length];
        byte key = 0x4f;
        output[0] = (byte)(key ^ input[0]);
        for (int i = 1; i< input.Length; ++i)
        {
            output[i] = (byte)(output[i - 1] ^ (key ^ input[i]));
        }
        return output;
    }
    public static byte[] Decrypt(byte[] input)
    {
        byte[] output = new byte[input.Length];
        byte key = 0x4f;
        for (int i = input.Length - 1; i > 0; --i)
        {
            output[i] = (byte)((input[i] ^ input[i-1]) ^ key);
        }
        output[0] = (byte)(key ^ input[0]);
        return output;
    }

}
