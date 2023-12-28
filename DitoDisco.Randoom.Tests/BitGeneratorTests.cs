namespace DitoDisco.Randoom.Tests;

public class BitGeneratorTests {

    BitGenerator rand = new Rule30Random();

    [Test]
    public void SampleTest() {
        // Formatting chosen to fit nicely in an 80 column wide terminal.

        TestContext.WriteLine($"{nameof(rand.NextDouble)} sample:");

        for(int i = 0; i < 6; i++) {
            for(int j = 0; j < 4; j++) {
                TestContext.Write(rand.NextDouble().ToString("F16"));
                TestContext.Write("  ");
            }
            TestContext.WriteLine();
        }

        TestContext.WriteLine($"{nameof(rand.NextSingle)} sample:");

        for(int i = 0; i < 4; i++) {
            for(int j = 0; j < 6; j++) {
                TestContext.Write(rand.NextSingle().ToString("F8"));
                TestContext.Write("  ");
            }
            TestContext.WriteLine();
        }

        Assert.Pass();
    }


    static readonly int TEST_RUNS = 1024;

    [Test]
    public void DoubleTest() {
        double sum = 0;

        for(int i = 0; i < TEST_RUNS; i++) {
            double d = rand.NextDouble();
            Assert.That(d, Is.GreaterThanOrEqualTo(0.0));
            Assert.That(d, Is.LessThan(1.0));

            sum += d;
        }

        double avg = sum / TEST_RUNS;
        TestContext.WriteLine($"Average of {TEST_RUNS} {nameof(rand.NextDouble)}s: {avg}");
    }

    [Test]
    public void SingleTest() {
        float sum = 0;

        for(int i = 0; i < TEST_RUNS; i++) {
            float f = rand.NextSingle();
            Assert.That(f, Is.GreaterThanOrEqualTo(0.0));
            Assert.That(f, Is.LessThan(1.0));

            sum += f;
        }

        float avg = sum / TEST_RUNS;
        TestContext.WriteLine($"Average of {TEST_RUNS} {nameof(rand.NextSingle)}s: {avg}");
    }
}
