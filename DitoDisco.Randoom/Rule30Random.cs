using System;


namespace DitoDisco.Randoom {

    /// <summary>
    /// Uses the Rule 30 elementary cellular automaton to generate reasonably random bits. The only reason you might want to use this over <see cref="System.Random"/> is to generate identical sequences cross-platform.
    /// </summary>
    public class Rule30Random : BitGenerator {

        /// <summary>
        /// Opaque object representing the state of a <see cref="Rule30Random"/> instance.
        /// </summary>
        public class State {
            internal bool[] bits;
            internal int nextBitIndex;

            internal State(bool[] bits, int nextBitIndex) {
                this.bits = bits;
                this.nextBitIndex = nextBitIndex;
            }
        }

        /// <summary>
        /// Small, opaque object representing the state of a <see cref="Rule30Random"/> instance.
        /// </summary>
        public class CompactState {
            internal byte[] bytes;
            internal int nextBitIndex;

            internal CompactState(byte[] bytes, int nextBitIndex) {
                this.bytes = bytes;
                this.nextBitIndex = nextBitIndex;
            }
        }



        const int DEFAULT_BIT_SPACING = 8;
        const int DEFAULT_SIZE = 255;

        //

        readonly int bufferWidth;
        readonly int stateLength;

        /// <summary>Stride of <see cref="nextBitIndex"/>.</summary>
        readonly int bitSpacing;
        /// <summary>Maximum value of <see cref="nextBitIndex"/>. When it exceeds this, the next state will be computed and the next bit index is reset to 0.</summary>
        readonly int maxBitIndex;

        int CompactStateByteCount => Math.DivRem(stateLength, 8, out int remainder) + (remainder == 0 ? 0 : 1);


        /// <summary>Index into <see cref="currentBuffer"/>. This is what the next bit will be.</summary>
        int nextBitIndex = 0;

        bool[] currentBuffer;
        bool[] nextBuffer;



        /// <summary>
        /// Initializes a new Rule 30 pseudorandom number generator, with a state width of <paramref name="size"/> bits, and seeds it using <paramref name="seed"/>.
        /// </summary>
        /// <param name="seed">Completely determines the sequence of randomness that will be generated. A seed of 0 results in "<see cref="UInt64.MaxValue"/> + 1" being used instead, since an empty state would just keep being empty and not generate anything.</param>
        /// <param name="size">Size of one state. Must be positive. Using an even number is not recommended, because the cellular automaton is stable if the state is 010101... repeating.</param>
        /// <param name="bitSpacing">Stride of bits picked out of the cellular automaton state. Higher values make subsequent pseudorandom values less correlated, but also require advancing the state more often.</param>
        public Rule30Random(ulong seed, int size = DEFAULT_SIZE, int bitSpacing = DEFAULT_BIT_SPACING) {
            if(size <= 0) throw new ArgumentOutOfRangeException(nameof(size), "Size must be positive.");
            if(bitSpacing <= 0) throw new ArgumentOutOfRangeException(nameof(bitSpacing), "Bit spacing must be positive.");

            stateLength = size;
            bufferWidth = size + 2;

            this.bitSpacing = bitSpacing;

            maxBitIndex = (stateLength / bitSpacing) * bitSpacing;

            currentBuffer = new bool[bufferWidth]; // Genuis: pad the buffers by 2 to avoid boundary checks in the advance loop
            nextBuffer = new bool[bufferWidth];

            Reseed(seed);
        }

        /// <summary>
        /// Initializes a new instance with reasonable parameters and uses the current time as the seed.
        /// </summary>
        public Rule30Random() : this((ulong)DateTime.Now.Ticks) { }



        public override bool NextBit() {
            bool bit = currentBuffer[1 + nextBitIndex];

            nextBitIndex += bitSpacing;

            if(nextBitIndex > maxBitIndex) {
                nextBitIndex = 0;
                AdvanceState();
            }

            return bit;
        }



        // Internal state manipulation

        private Span<bool> GetStateSpan() => currentBuffer.AsSpan().Slice(1, stateLength);

        private void SwapBuffers() {
            (currentBuffer, nextBuffer) = (nextBuffer, currentBuffer);
        }


        private void AdvanceState() {
            // Pre-wrapping to avoid boundary checks in the loop:
            // The 0th element is actually the last-in-bounds, and the last element is the first-in-bounds
            currentBuffer[0] = currentBuffer[bufferWidth - 2];
            currentBuffer[bufferWidth - 1] = currentBuffer[1];

            bool left = currentBuffer[0];
            bool mid = currentBuffer[1];
            for(int i = 2; i < bufferWidth; i++) {
                bool right = currentBuffer[i];
                nextBuffer[i] = left ^ (mid || right);
                (left, mid) = (mid, right); // <--
            }

            SwapBuffers();
        }


        private void InternalWriteSeed(ulong seed) {
            if(seed == 0) {
                // Special case: empty seed
                int i = sizeof(ulong) * 8 + 1;
                currentBuffer[1 + (i % stateLength)] = true;
                return;
            }

            // Copy the bits of the seed
            for(int i = 0; i < sizeof(ulong) * 8; i++) {
                currentBuffer[1 + (i % stateLength)] = (seed & (1u << i)) != 0;
            }
        }


        // State in/out

        /// <summary>
        /// Seeds the state using a number. Seeding with the same value will result in subsequent bits being the same.
        /// </summary>
        /// <param name="propagate">Whether to do some state advancements after seeding to increase chaos before actually producing bits. This is heavily recommended.</param>
        public void Reseed(ulong seed, bool propagate = true) {
            Array.Fill(currentBuffer, false);
            InternalWriteSeed(seed);

            // Let the seed propagate
            if(propagate) for(int i = 0; i < stateLength * 2; i++) AdvanceState();
        }

        /// <summary>
        /// Creates an opaque object that represents the internal state of the generator. Can be imported later to get the same results from <see cref="NextBit"/>.
        /// </summary>
        public State ExportState() {
            bool[] stateCopy = new bool[stateLength];
            GetStateSpan().CopyTo(stateCopy);

            return new State(stateCopy, nextBitIndex);
        }

        /// <summary>
        /// Imports a state exported by <see cref="ExportState"/>. The exporting instance must have the same state length (determined at construction) as this one.
        /// </summary>
        public void ImportState(State state) {
            if(state.bits.Length != stateLength) throw new ArgumentOutOfRangeException(nameof(state), "This state contains a different amount of bits than expected. It might have been generated by an instance with a different state length, and thus cannot be used with this instance.");
            if(state.nextBitIndex > maxBitIndex) throw new ArgumentOutOfRangeException(nameof(state), "This state's next bit index is greater than what's allowed in this instance. It might have been generated by an instance with a larger state length, and thus cannot be used with this instance.");

            state.bits.CopyTo(GetStateSpan());
            nextBitIndex = state.nextBitIndex;
        }


        /// <summary>
        /// Creates an opaque object that memory-efficiently represents the internal state of the generator, at the expense of taking a bit longer to create. Can be imported later to get the same results from <see cref="NextBit"/>.
        /// </summary>
        public CompactState ExportCompactState() {
            byte[] bytes = new byte[CompactStateByteCount];

            Span<bool> stateSpan = GetStateSpan();

            int n = (stateLength / 8) * 8;
            int j = 0;
            for(int i = 0; i < n; i += 8) {
                bytes[j++] = (byte)(
                    (stateSpan[i  ] ? 1 : 0) << 0 |
                    (stateSpan[i+1] ? 1 : 0) << 1 |
                    (stateSpan[i+2] ? 1 : 0) << 2 |
                    (stateSpan[i+3] ? 1 : 0) << 3 |
                    (stateSpan[i+4] ? 1 : 0) << 4 |
                    (stateSpan[i+5] ? 1 : 0) << 5 |
                    (stateSpan[i+6] ? 1 : 0) << 6 |
                    (stateSpan[i+7] ? 1 : 0) << 7
                );
            }

            byte last = 0;
            for(int i = n; i < stateLength; i++) {
                last |= (byte)((stateSpan[i] ? 1 : 0) << (i - n));
            }

            if(last != 0) bytes[^1] = last;

            return new CompactState(bytes, nextBitIndex);
        }


        /// <summary>
        /// Imports a compact state exported by <see cref="ExportCompactState"/>. The exporting instance must have the same state length (determined at construction) as this one.
        /// </summary>
        public void ImportCompactState(CompactState state) {
            if(state.bytes.Length != CompactStateByteCount) throw new ArgumentOutOfRangeException(nameof(state), "This compact state contains a different amount of bits than expected. It might have been generated by an instance with a different state length, and thus cannot be used with this instance.");
            if(state.nextBitIndex > maxBitIndex) throw new ArgumentOutOfRangeException(nameof(state), "This compact state's next bit index is greater than what's allowed in this instance. It might have been generated by an instance with a larger state length, and thus cannot be used with this instance.");

            Span<bool> stateSpan = GetStateSpan();

            int bit = 0;
            int byteIndex = 0;
            for(int i = 0; i < stateSpan.Length; i++) {
                stateSpan[i] = ((state.bytes[byteIndex] >> bit) & 0b1) != 0;

                bit = (bit + 1) % 8;
                if(bit == 0) byteIndex++; // Just rolled over, increment
            }

            nextBitIndex = state.nextBitIndex;
        }


        /// <summary>
        /// Manually advances the cellular automaton to the next state. Calling this is completely unneccessary.
        /// </summary>
        public void ManuallyAdvanceState() {
            nextBitIndex = 0;
            AdvanceState();
        }

    }

}
