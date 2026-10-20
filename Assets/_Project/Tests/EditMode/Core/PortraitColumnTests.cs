using NUnit.Framework;
using Template.Core.Ui;

namespace Template.Core.Tests
{
    public class PortraitColumnTests
    {
        [TestCase(1080, 1920)] // 9:16 phone
        [TestCase(1080, 2400)] // 20:9 phone
        [TestCase(1536, 2048)] // 3:4 tablet, the widest tested shape
        public void Phones_and_tablets_keep_the_whole_screen(int width, int height)
        {
            Assert.AreEqual((0, width), PortraitColumn.For(width, height));
        }

        [Test]
        public void Landscape_window_keeps_a_centred_3_by_4_column()
        {
            Assert.AreEqual((420, 1080), PortraitColumn.For(1920, 1440));
            var (x, width) = PortraitColumn.For(1262, 673); // a dragged-out desktop window
            Assert.AreEqual(505, width);
            Assert.AreEqual(378, x);
        }

        [Test]
        public void Square_window_is_cut_to_the_column_too()
        {
            Assert.AreEqual((112, 675), PortraitColumn.For(900, 900));
        }
    }
}
