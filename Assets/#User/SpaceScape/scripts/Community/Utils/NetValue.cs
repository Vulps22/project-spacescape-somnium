using SomniumSpace.Network.Bridge;
using System;
using UnityEngine;
using UnityEngine.XR;

namespace SomniumSpace.Worlds.SpaceScape.Community
{
    /// <summary>
    /// Read and write values in a NetworkBridgeData
    /// Since the data is stored as an array of int, this class provides methods to read and write bytes, shorts, floats and ints.
    /// </summary>
    public class NetValue
    {
        public int IntCount => _networkBridgeIntegers.IntCount;

        private NetworkBridgeData _networkBridgeIntegers;
        private readonly int _index;
        private readonly int _subIndex;

        /// <summary>
        /// Create a reader/writer for value in a NetworkBridgeData.
        /// Be careful, you need to make sure the NetworkBridgeData contain enough slots for the index you want to use.
        /// E.g. NetworkBridgeData1 contain 1 slot, NetworkBridgeData2 contain 2 slots, NetworkBridgeData4 contain 4 slots, etc.
        /// </summary>
        /// <param name="networkBridgeIntegers"> Target NetworkBridgeData to read/write memory from/to </param>
        /// <param name="index"> Index of the target 32Bits data in the NetworkBridgeData</param>
        /// <param name="subIndex"> Only necessary for bool, byte and shorts
        /// - For bool: position of the bit in the 32Bits data [0 to 31] inclusively
        /// - For byte: position of the byte in the 32Bits data [0 to 3] inclusively
        /// - For short: position of the short in the 32Bits data [0 to 1] inclusively
        /// </param>
        public NetValue(NetworkBridgeData networkBridgeIntegers, int index, int subIndex=0)
        {
            _networkBridgeIntegers = networkBridgeIntegers;
            _index = index;
            _subIndex = subIndex;
        }

        public bool GetBool(int boolPosition = 0)
        {
            int intValue = _networkBridgeIntegers.IntArray.Get(_index);
            return ((intValue >> boolPosition) & 1) == 1;
        }

        public void SetBool(bool value)
        {
            int data = _networkBridgeIntegers.IntArray.Get(_index);
            if (value)
                data |= (1 << _subIndex); // Set bit to 1
            else
                data &= ~(1 << _subIndex); // Set bit to 0
            _networkBridgeIntegers.IntArray.Set(_index, data);
        }

        public byte GetByte()
        {
            int intValue = _networkBridgeIntegers.IntArray.Get(_index);
            return (byte)((intValue >> (_subIndex * 8)) & 0xFF);
        }

        public void SetByte(byte value)
        {
            int intValue = _networkBridgeIntegers.IntArray.Get(_index);
            int shift = _subIndex * 8;
            intValue = (short)((intValue & ~(0xFF << shift)) | (value << shift));
            _networkBridgeIntegers.IntArray.Set(_index, intValue);
        }

        /// <summary>
        /// </summary>
        /// <param name="shortPosition"> Position of the short in the memory slot [0 to 1] </param>
        public short GetShort()
        {
            int intValue = _networkBridgeIntegers.IntArray.Get(_index);
            return (short)((intValue >> (_subIndex * 16)) & 0xFFFF);
        }

        /// <summary>
        /// </summary>
        /// <param name="value"> Value to update </param>
        /// <param name="shortPosition"> Position of the short in the memory slot [0 to 1] </param>
        public void SetShort(short value)
        {
            int intValue = _networkBridgeIntegers.IntArray.Get(_index);
            int shift = _subIndex * 16;
            intValue = (short)((intValue & ~(0xFFFF << shift)) | (value << shift));
            _networkBridgeIntegers.IntArray.Set(_index, intValue);
        }

        public float GetFloat()
        {
            int intValue = _networkBridgeIntegers.IntArray.Get(_index);
            return BitConverter.ToSingle(BitConverter.GetBytes(intValue), 0);
        }

        public void SetFloat(float value)
        {
            int intValue = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            _networkBridgeIntegers.IntArray.Set(_index, intValue);
        }

        public int GetInt()
        {
            return _networkBridgeIntegers.IntArray.Get(_index);
        }

        public void SetInt(int value)
        {
            _networkBridgeIntegers.IntArray.Set(_index, value);
        }

        public Color32 GetColor32()
        {
            byte[] colorRaw = Get4Bytes();
            return new Color32(colorRaw[0], colorRaw[1], colorRaw[2], colorRaw[3]);
        }

        public void SetColor32(Color32 color)
        {
            Set4Bytes(new byte[4] { color[0], color[1], color[2], color[3] });
        }

        public byte[] Get4Bytes()
        {
            return BitConverter.GetBytes(_networkBridgeIntegers.IntArray.Get(_index));
        }
        
        /// <summary>
        /// Require exactly 4 bytes
        /// </summary>
        public void Set4Bytes(byte[] bytes)
        {
            _networkBridgeIntegers.IntArray.Set(_index, BitConverter.ToInt32(bytes));
        }

        public short[] Get2Short()
        {
            int intValue = _networkBridgeIntegers.IntArray.Get(_index);
            return new short[] { (short)intValue, (short)(intValue >> 16) };
        }

        /// <summary>
        /// Require exactly 2 shorts
        /// </summary>
        public void Set2Short(short[] shorts)
        {
            int intValue = (ushort)shorts[0] | ((ushort)shorts[1] << 16);
            _networkBridgeIntegers.IntArray.Set(_index, intValue);
        }
    }
}
