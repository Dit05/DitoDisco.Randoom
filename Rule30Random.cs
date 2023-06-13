using System;


namespace DitoDisco.Random {

    public class Rule30Random : System.Random {
        // ours: 1461.5 ms
        // System.Random: 71.2 ms :(


        readonly int bufferWidth;
        readonly int stateLength;


        bool[] currentBuffer;
        bool[] nextBuffer;


        const int DEFAULT_BIT_SPACING = 8;

        readonly int bitSpacing;
        readonly int stateBitCapacity;
        int nextStateBitIndex = 0;


        /// <summary>
        /// The length of one state, in bits.
        /// </summary>
        public int StateLength => stateLength;

        /// <summary>
        /// The amount of bytes used by <see cref="ExportCompactState(Span{byte})"/> and <see cref="ImportCompactState(Span{byte})"/>.
        /// </summary>
        public int CompactStateLength => CeilingIntDivide(stateLength, 8);


        /// <summary>
        /// Initializes a new Rule 30 pseudorandom number generator, with a state width of <paramref name="size"/> bits, and seeds it using <paramref name="seed"/>.
        /// </summary>
        /// <param name="size">Size of one state. This determines the value of <see cref="StateLength"/>. Must be positive. Using an even number is not recommended, because the cellular automaton is stable if the state is 010101... repeating.</param>
        /// <param name="seed">Completely determines the sequence of randomness that will be generated. For technical reasons, a seed of 0 results in 1 being used instead.</param>
        /// <param name="bitSpacing">Interval of bits picked out of the cellular automaton state. Higher values make subsequent pseudorandom values less correlated, but also require more computation to advance the state.</param>
        public Rule30Random(int size, ulong seed, int bitSpacing = DEFAULT_BIT_SPACING) {
            if(size <= 0) throw new ArgumentOutOfRangeException(nameof(size), "Size must be positive.");
            if(bitSpacing <= 0) throw new ArgumentOutOfRangeException(nameof(bitSpacing), "Bit spacing must be positive.");

            stateLength = size;
            bufferWidth = size + 2;

            this.bitSpacing = bitSpacing;

            stateBitCapacity = CeilingIntDivide(stateLength, bitSpacing);

            currentBuffer = new bool[bufferWidth]; // Genuis: pad the buffers by 2 to avoid boundary checks
            nextBuffer = new bool[bufferWidth];

            // Initialize the first state by copying the bits of the seed
            if(seed == 0) seed++;

            int count = Math.Min(stateLength, sizeof(ulong) * 8);
            for(int i = 1; i < count + 1; i++) {
                currentBuffer[i] = (seed & (1u << i)) != 0;
            }

            // Let the seed propagate
            for(int i = 0; i < stateLength * 2; i++) AdvanceState();
        }

        /// <summary>
        /// Initializes a new instance and uses the current time as the seed.
        /// </summary>
        public Rule30Random(int width) : this(width, (ulong)DateTime.Now.Ticks) { }



        // Helpers

        private static int CeilingIntDivide(int dividend, int divisor) {
            int remainder = Math.DivRem(dividend, divisor, out int result);
            return (remainder == 0) ? result : result + 1;
        }


        private Span<bool> GetStateSpan() => currentBuffer.AsSpan().Slice(1, stateLength);


        private void GiveUp() {
#if DEBUG
            throw new Exception("Couldn't make a random number even after very many attempts.");
#endif
        }


        // Internal state manipulation

        private void SwapBuffers() {
            var temp = currentBuffer;
            currentBuffer = nextBuffer;
            nextBuffer = temp;
        }


        private void AdvanceState() {
            // Wrapping around: The 0th element is actually the last-in-bounds, and the last element is the first-in-bounds
            currentBuffer[0] = currentBuffer[bufferWidth - 2];
            currentBuffer[bufferWidth - 1] = currentBuffer[1];

            for(int i = 1; i < bufferWidth - 1; i++) {
                bool left  = currentBuffer[i - 1];
                bool mid   = currentBuffer[i    ];
                bool right = currentBuffer[i + 1];
                nextBuffer[i] = left ^ (mid || right);
            }

            SwapBuffers();
        }


        // State in/out

        /// <summary>
        /// Copies the current state into the provided span.
        /// </summary>
        /// <param name="state">Destination for the state. Its length must be at exactly <see cref="StateLength"/>.</param>
        public void ExportState(Span<bool> state) {
            if(state.Length != StateLength) throw new ArgumentException(nameof(state), $"The destination span's length must be exactly {nameof(StateLength)}.");

            GetStateSpan().CopyTo(state);
        }

        /// <summary>
        /// Like <see cref="ExportCompactState(Span{byte})"/> but creates an array too.
        /// </summary>
        public bool[] ExportState() {
            bool[] array = new bool[stateLength];
            ExportState(array.AsSpan());
            return array;
        }

        /// <summary>
        /// Sets the full RNG state to the provided one.
        /// </summary>
        /// <param name="state">The state to import. Its length must be exactly <see cref="StateLength"/>.</param>
        public void ImportState(Span<bool> state) {
            if(state.Length != stateLength) throw new ArgumentException(nameof(state), $"The provided state's length must be exactly {nameof(StateLength)}.");

            state.CopyTo(GetStateSpan());
        }


        /// <summary>
        /// Copies the current state into the bits of a span of bytes, filling from least to most significant bit.
        /// </summary>
        /// <param name="dest">Destination for the compact state. Its length must be at exactly <see cref="CompactStateLength"/>.</param>
        public void ExportCompactState(Span<byte> dest) {
            if(dest.Length != CompactStateLength) throw new ArgumentException(nameof(dest), $"The destination span's length must be exactly {nameof(CompactStateLength)}.");

            int stateI = 0;
            int stateBit = 0;
            for(int i = 1; i < bufferWidth - 1; i++) {
                bool cellOn = currentBuffer[i];

                if(cellOn) dest[stateI] |= (byte)(1 << stateBit);

                stateBit++;
                if(stateBit >= 8) {
                    stateBit = 0;
                    stateI++;
                    dest[stateI] = 0;
                }
            }
        }

        /// <summary>
        /// Like <see cref="ExportCompactState(Span{byte})"/> but creates an array too.
        /// </summary>
        public byte[] ExportCompactState() {
            byte[] array = new byte[CompactStateLength];
            ExportCompactState(array);
            return array;
        }

        /// <summary>
        /// Sets the state using an array of uints made by <see cref="ExportCompactState(Span{byte})"/>.
        /// </summary>
        /// <param name="state">The compact state to import. Its length must be exactly <see cref="CompactStateLength"/>.</param>
        public void ImportCompactState(Span<byte> state) {
            if(state.Length != CompactStateLength) throw new ArgumentException(nameof(state), $"The destination span's length must be at least {nameof(CompactStateLength)}.");

            int stateI = 0;
            int stateBit = 0;
            for(int i = 1; i < bufferWidth - 1; i++) {
                bool cellOn = (state[stateI] & (byte)(1 << stateBit)) != 0;

                currentBuffer[i] = cellOn;

                stateBit++;
                if(stateBit > 8) {
                    stateBit = 0;
                    stateI++;
                }
            }
        }


        /// <summary>
        /// Manually advances the cellular automaton to the next state. Calling this is completely unneccessary.
        /// </summary>
        public void ManuallyAdvanceState() {
            nextStateBitIndex = 0;
            AdvanceState();
        }


        /// <summary>
        /// Returns true or false (pseudo)randomly.
        /// </summary>
        public bool NextBit() {
            bool bit = currentBuffer[nextStateBitIndex * bitSpacing];

            nextStateBitIndex++;

            if(nextStateBitIndex >= stateBitCapacity) {
                nextStateBitIndex = 0;
                AdvanceState();
            }

            return bit;
        }


        // Overrides of System.Random

        /// <summary>
        /// Returns a pseudorandom double-precision number that is at least 0 and less than 1.
        /// </summary>
        public override double NextDouble() => NextUInt64(ulong.MaxValue) / (float)ulong.MaxValue;

        /// <summary>
        /// Returns a pseudorandom single-precision number that is at least 0 and less than 1.
        /// </summary>
        public override float NextSingle() => NextUInt64(uint.MaxValue) / (float)uint.MaxValue;


        /// <summary>
        /// Returns a pseudorandom 64-bit unsigned integer with the first <paramref name="bitCount"/> bits randomized.
        /// </summary>
        public ulong NextUInt64WithBits(int bitCount) {
            if(bitCount < 0 || bitCount > sizeof(ulong) * 8) throw new ArgumentOutOfRangeException(nameof(bitCount));

            ulong num = 0;
            for(int i = 0; i < bitCount; i++) {
                if(NextBit()) num |= 1UL << i;
            }

            return num;
        }


        /// <summary>
        /// Returns a pseudorandom <see cref="UInt64"/> between 0 and <see cref="UInt64.MaxValue"/>, inclusive. (so basically, any value)
        /// </summary>
        public ulong NextUInt64() => NextUInt64WithBits(sizeof(uint) * 8);

        /// <summary>
        /// Returns a pseudorandom <see cref="UInt64"/> that is at least 0 and smaller than <paramref name="maxValue"/>.
        /// </summary>
        /// <param name="maxValue">Exclusive upper bound of the value.</param>
        public ulong NextUInt64(ulong maxValue) {
            if(maxValue <= 0) throw new ArgumentOutOfRangeException(nameof(maxValue), "Maximum value must be positive.");

            int bitCount = Math.ILogB(maxValue) + 1;

            int safetyCounter = 8192;
            while(safetyCounter-- > 0) {
                ulong possibleValue = NextUInt64WithBits(bitCount);

                if(possibleValue < maxValue) return possibleValue;
            }

            GiveUp();
            return 0;
        }

        /// <summary>
        /// Returns a pseudorandom <see cref="UInt64"/> that is at least <paramref name="minValue"/> but less than <paramref name="maxValue"/>.
        /// </summary>
        /// <param name="minValue">Inclusive lower bound of the value.</param>
        /// <param name="maxValue">Exclusive upper bound of the value.</param>
        public ulong NextUInt64(ulong minValue, ulong maxValue) {
            if(maxValue < minValue) throw new ArgumentOutOfRangeException(nameof(maxValue), "Maximum value must not be smaller than the minimum value.");
            if(maxValue == minValue) return minValue;

            return minValue + NextUInt64(maxValue - minValue);
        }


        /// <summary>
        /// Returns a pseudorandom <see cref="Int64"/> between 0 and <see cref="Int64.MaxValue"/>, inclusive.
        /// </summary>
        public override long NextInt64() => (long)NextUInt64WithBits(sizeof(ulong) * 8 - 1);


        /// <summary>
        /// Returns a pseudorandom <see cref="Int64"/> that is at least 0 and smaller than <paramref name="maxValue"/>.
        /// </summary>
        /// <param name="maxValue">Exclusive upper bound of the value.</param>
        public override long NextInt64(long maxValue) => (long)NextUInt64((ulong)maxValue);


        /// <summary>
        /// Returns a pseudorandom <see cref="Int64"/> that is at least <paramref name="minValue"/> but less than <paramref name="maxValue"/>.
        /// </summary>
        /// <param name="minValue">Inclusive lower bound of the value.</param>
        /// <param name="maxValue">Exclusive upper bound of the value.</param>
        public override long NextInt64(long minValue, long maxValue) {
            if(maxValue < minValue) throw new ArgumentOutOfRangeException(nameof(maxValue), "Maximum value must not be smaller than the minimum value.");
            if(maxValue == minValue) return minValue;

            return minValue + NextInt64(maxValue - minValue);
        }


        /// <summary>
        /// Returns a pseudorandom <see cref="Int32"/> between 0 and <see cref="Int32.MaxValue"/>, inclusive.
        /// </summary>
        public override int Next() => (int)NextUInt64WithBits(sizeof(int) * 8 - 1);


        /// <summary>
        /// Returns a pseudorandom integer that is at least 0 and less than <paramref name="maxValue"/>.
        /// </summary>
        /// <param name="maxValue">Exclusive upper bound of the value.</param>
        public override int Next(int maxValue) {
            return (int)NextUInt64WithBits(sizeof(int) * 8 - 1);
        }

        /// <summary>
        /// Returns a pseudorandom <see cref="Int32"/> that is at least <paramref name="minValue"/> but less than <paramref name="maxValue"/>.
        /// </summary>
        /// <param name="minValue">Inclusive lower bound of the value.</param>
        /// <param name="maxValue">Exclusive upper bound of the value.</param>
        public override int Next(int minValue, int maxValue) {
            if(maxValue < minValue) throw new ArgumentOutOfRangeException(nameof(maxValue), "Maximum value must not be smaller than the minimum value.");
            if(maxValue == minValue) return minValue;

            return minValue + Next(maxValue - minValue);
        }


        /// <summary>
        /// Returns a pseudorandom byte.
        /// </summary>
        public byte NextByte() {
            return (byte)(
                (NextBit() ? (1 << 0) : 0) |
                (NextBit() ? (1 << 1) : 0) |
                (NextBit() ? (1 << 2) : 0) |
                (NextBit() ? (1 << 3) : 0) |
                (NextBit() ? (1 << 4) : 0) |
                (NextBit() ? (1 << 5) : 0) |
                (NextBit() ? (1 << 6) : 0) |
                (NextBit() ? (1 << 7) : 0)
            );
        }


        /// <summary>
        /// Fills the provided byte array with random values.
        /// </summary>
        public override void NextBytes(byte[] buffer) {
            for(int i = 0; i < buffer.Length; i++) {
                buffer[i] = NextByte();
            }
        }

        /// <summary>
        /// Fills the provided span with random values.
        /// </summary>
        public override void NextBytes(Span<byte> buffer) {
            for(int i = 0; i < buffer.Length; i++) {
                buffer[i] = NextByte();
            }
        }

    }

}
