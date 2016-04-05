﻿#region Used namespaces

using System;
using System.Security.Cryptography;
using System.Text;

#endregion

namespace KGySoft.Libraries
{
    /// <summary>
    /// Implementation of CRC32 hash algorithm.
    /// </summary>
    public sealed class Crc32: HashAlgorithm
    {
        #region Constants

        /// <summary>
        /// Default polinomial for the <see cref="Crc32"/> hash algorithm.
        /// </summary>
        public const uint DefaultPolynomial = 0xedb88320;

        /// <summary>
        /// Default seed for the <see cref="Crc32"/> hash algorithm.
        /// </summary>
        public const uint DefaultSeed = UInt32.MaxValue;

        #endregion

        #region Fields

        #region Static Fields

        private static uint[] defaultTable;

        #endregion

        #region Instance Fields

        private uint hash;

        private readonly uint seed;

        private readonly uint[] table;

        #endregion

        #endregion

        #region Properties

        /// <summary>
        /// Gets the size, in bits, of the computed hash code.
        /// </summary>
        /// <returns>
        /// The size, in bits, of the computed hash code.
        /// </returns>
        public override int HashSize
        {
            get { return 32; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="Crc32"/> class with default settings.
        /// </summary>
        public Crc32()
        {
            table = InitializeTable(DefaultPolynomial);
            seed = DefaultSeed;
            Initialize();
        }

        /// <summary>
        /// Initializes a new instance of <see cref="Crc32"/> class with custom settings.
        /// </summary>
        public Crc32(uint polynomial, uint seed)
        {
            table = InitializeTable(polynomial);
            this.seed = seed;
            Initialize();
        }

        #endregion

        #region Methods

        #region Static Methods

        #region Public Methods

        /// <summary>
        /// Creates an instance of the default implementation of the <see cref="Crc32"/> hash algorithm.
        /// </summary>
        /// <remarks>Unlike classes in <see cref="System.Security.Cryptography"/> namespace this
        /// method does not use <see cref="CryptoConfig"/> class.</remarks>
        public new static Crc32 Create()
        {
            return new Crc32();
        }

        /// <summary>
        /// Creates an instance of the specified implementation of the <see cref="Crc32"/> hash algorithm.
        /// </summary>
        /// <remarks>Unlike classes in <see cref="System.Security.Cryptography"/> namespace this
        /// method does not use <see cref="CryptoConfig"/> class.</remarks>
        public static Crc32 Create(uint polynomial, uint seed)
        {
            return new Crc32(polynomial, seed);
        }

        /// <summary>
        /// Calculates the CRC32 hash of given string.
        /// </summary>
        public static uint CalculateHash(string s)
        {
            byte[] buffer = Encoding.Unicode.GetBytes(s);
            return ~CalculateHash(InitializeTable(DefaultPolynomial), DefaultSeed, buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Calculates the CRC32 hash of given data.
        /// </summary>
        public static uint CalculateHash(byte[] buffer)
        {
            return ~CalculateHash(InitializeTable(DefaultPolynomial), DefaultSeed, buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Calculates the CRC32 hash of given data.
        /// </summary>
        public static uint CalculateHash(byte[] buffer, int start, int length)
        {
            return ~CalculateHash(InitializeTable(DefaultPolynomial), DefaultSeed, buffer, start, length);
        }

        /// <summary>
        /// Calculates the CRC32 hash of given data.
        /// </summary>
        public static uint CalculateHash(uint seed, byte[] buffer)
        {
            return ~CalculateHash(InitializeTable(DefaultPolynomial), seed, buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Calculates the CRC32 hash of given data.
        /// </summary>
        public static uint CalculateHash(uint polynomial, uint seed, byte[] buffer)
        {
            return ~CalculateHash(InitializeTable(polynomial), seed, buffer, 0, buffer.Length);
        }

        #endregion

        #region Private Methods

        private static uint[] InitializeTable(uint polynomial)
        {
            if (polynomial == DefaultPolynomial && defaultTable != null)
                return defaultTable;

            uint[] createTable = new uint[256];
            for (int i = 0; i < 256; i++)
            {
                uint entry = (uint)i;
                for (int j = 0; j < 8; j++)
                    if ((entry & 1) == 1)
                        entry = (entry >> 1) ^ polynomial;
                    else
                        entry = entry >> 1;
                createTable[i] = entry;
            }

            if (polynomial == DefaultPolynomial)
                defaultTable = createTable;

            return createTable;
        }

        private static uint CalculateHash(uint[] table, uint seed, byte[] buffer, int start, int size)
        {
            uint crc = seed;
            int end = start + size;
            for (int i = start; i < end; i++)
                unchecked
                {
                    crc = (crc >> 8) ^ table[buffer[i] ^ crc & 0xff];
                }
            return crc;
        }

        private static byte[] UInt64ToBigEndianBytes(uint x)
        {
            return new byte[]
            {
                (byte)((x >> 24) & 0xff),
                (byte)((x >> 16) & 0xff),
                (byte)((x >> 8) & 0xff),
                (byte)(x & 0xff)
            };
        }

        #endregion

        #endregion

        #region Instance Methods

        #region Public Methods

        /// <summary>
        /// Initializes the <see cref="Crc32"/> instance.
        /// </summary>
        public override void Initialize()
        {
            hash = seed;
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Routes data written to the object into the hash algorithm for computing the hash.
        /// </summary>
        protected override void HashCore(byte[] buffer, int start, int length)
        {
            hash = CalculateHash(table, hash, buffer, start, length);
        }

        /// <summary>
        /// Finalizes the hash computation after the last data is processed by the cryptographic stream object.
        /// </summary>
        /// <returns>
        /// The computed hash code.
        /// </returns>
        protected override byte[] HashFinal()
        {
            byte[] hashBuffer = UInt64ToBigEndianBytes(~hash);
            HashValue = hashBuffer;
            return hashBuffer;
        }

        #endregion

        #endregion

        #endregion
    }
}
