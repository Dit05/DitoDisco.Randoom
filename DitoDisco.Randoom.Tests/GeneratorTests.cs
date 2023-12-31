namespace DitoDisco.Randoom.Tests;

[TestFixture(typeof(Rule30Random))]
[TestFixture(typeof(RandomGenerator))]
public class GeneratorTests {

    private readonly BitGenerator gen;
    private readonly string idTag;


    public GeneratorTests(Type type) {
        gen = (BitGenerator)Activator.CreateInstance(type)!;
        idTag = $"[{gen.GetType().Name}]";
    }

    [Test]
    public void BitSample() {
        TestContext.WriteLine($"{idTag} Bit sample:");
        for(int i = 0; i < 24; i++) {
            for(int j = 0; j < 60 /* 60 has a lot of divisors, which should help patterns stand out. */; j++) {
                TestContext.Write(gen.NextBit() ? '1' : '0');
            }
            TestContext.WriteLine();
        }
    }

    [Test]
    public void BitSpeedTest() {
        System.Diagnostics.Stopwatch stopper = new System.Diagnostics.Stopwatch();

        const int COUNT = 100_000;

        stopper.Start();
        for(int i=0;i<COUNT;i++)gen.NextBit();//Make sure to keep this benchmarked part compact so that it runs faster !!
        stopper.Stop();


        TestContext.WriteLine($"{idTag} Generated {COUNT} bits in {stopper.Elapsed.TotalMilliseconds} ms.");
    }

}
