using System;


namespace DitoDisco.Randoom {

    /// <summary>
    /// Uses the Rule 30 elementary cellular automaton to generate reasonably random bits.
    /// </summary>
    public class Rule30Random : BitGenerator {
        // System.Random: 71.2 ms
        // ours: 1461.5 ms :(

        const int DEFAULT_BIT_SPACING = 8;
        const int DEFAULT_SIZE = 255;


        readonly int bufferWidth;
        readonly int stateLength;

        bool[] currentBuffer;
        bool[] nextBuffer;

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
        /// <param name="seed">Completely determines the sequence of randomness that will be generated. A seed of 0 results in 1 being used instead, since an empty state would just keep being empty and not generate anything.</param>
        /// <param name="size">Size of one state. This determines the value of <see cref="StateLength"/>. Must be positive. Using an even number is not recommended, because the cellular automaton is stable if the state is 010101... repeating.</param>
        /// <param name="bitSpacing">Interval of bits picked out of the cellular automaton state. Higher values make subsequent pseudorandom values less correlated, but also require more computation to advance the state.</param>
        public Rule30Random(ulong seed, int size = DEFAULT_SIZE, int bitSpacing = DEFAULT_BIT_SPACING) {
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
        /// Initializes a new instance with reasonable parameters and uses the current time as the seed.
        /// </summary>
        public Rule30Random() : this((ulong)DateTime.Now.Ticks) { }



        // Helpers

        private static int CeilingIntDivide(int dividend, int divisor) {
            int result = Math.DivRem(dividend, divisor, out int remainder);
            return (remainder == 0) ? result : result + 1;
        }


        private Span<bool> GetStateSpan() => currentBuffer.AsSpan().Slice(1, stateLength);


        // Internal state manipulation

        private void SwapBuffers() {
            (currentBuffer, nextBuffer) = (nextBuffer, currentBuffer);
        }


        private void AdvanceState() {
            // Pre-wrapping to avoid boundary checks in the loop:
            // The 0th element is actually the last-in-bounds, and the last element is the first-in-bounds
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
        /// Like <see cref="ExportCompactState(Span{byte})"/> but creates a new array instead of using an existing span.
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
        public void ImportState(ReadOnlySpan<bool> state) {
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
        /// Like <see cref="ExportCompactState(Span{byte})"/> but creates a new array instead of using an existing span.
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
        public void ImportCompactState(ReadOnlySpan<byte> state) {
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


        public override bool NextBit() {
            bool bit = currentBuffer[nextStateBitIndex * bitSpacing];

            nextStateBitIndex++;

            if(nextStateBitIndex >= stateBitCapacity) {
                nextStateBitIndex = 0;
                AdvanceState();
            }

            return bit;
        }



    }

}
