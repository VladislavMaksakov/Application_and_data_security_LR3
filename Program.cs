using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
namespace LsbSteganography
{
   class Program
   {
      static void Main(string[] args)
      {
         string originalImagePath = "/Users/vladislavmaksakov/Desktop/Learning/БПД/LR3/Encode.png";
         string stegoImagePath = "stego_result.png";
         string secretMessage = "Максаков Владислав Олександрович, ДН:05.03.2004, 6.04.121.013.22.2";

         Console.WriteLine("=== LSB Стеганографія (ImageSharp) ===");

         try
         {
            using (Image<Rgba32> image = Image.Load<Rgba32>(originalImagePath))
            {
               Console.WriteLine($"[INFO] Оригінал завантажено: {image.Width}x{image.Height}");
               Console.WriteLine("=== Етап 2: Приховування даних ===");
               HideMessage(image, secretMessage);
               image.SaveAsPng(stegoImagePath);
               Console.WriteLine($"[SUCCESS] Файл збережено: {stegoImagePath}");
            }
            Console.WriteLine("\n=== Етап 3: Витягування даних ===");
            using (Image<Rgba32> stegoImage = Image.Load<Rgba32>(stegoImagePath))
            {
               string extractedText = ExtractMessage(stegoImage);
               Console.WriteLine($"[RESULT] Розшифровано:\n{extractedText}");
            }
            var file1 = new System.IO.FileInfo(originalImagePath);
            var file2 = new System.IO.FileInfo(stegoImagePath);
            Console.WriteLine($"\n[INFO] Оригінал: {file1.Length} байт, Стеґо: {file2.Length} байт");
         }
         catch (System.IO.FileNotFoundException)
         {
            Console.WriteLine($"[ERROR] Файл не знайдено! Перевірте шлях: {originalImagePath}");
         }
         catch (Exception ex)
         {
            Console.WriteLine($"[ERROR] {ex.Message}");
         }
      }
      static void HideMessage(Image<Rgba32> img, string message)
      {
         byte[] messageBytes = Encoding.UTF8.GetBytes(message + "\0");
         int byteIndex = 0;
         int bitIndex = 0;
         for (int y = 0; y < img.Height; y++)
         {
            for (int x = 0; x < img.Width; x++)
            {
               if (byteIndex >= messageBytes.Length) return;
               Rgba32 pixel = img[x, y];
               pixel.R = EmbedBit(pixel.R, messageBytes, ref byteIndex, ref bitIndex);
               if (byteIndex < messageBytes.Length)
                  pixel.G = EmbedBit(pixel.G, messageBytes, ref byteIndex, ref bitIndex);
               if (byteIndex < messageBytes.Length)
                  pixel.B = EmbedBit(pixel.B, messageBytes, ref byteIndex, ref bitIndex);
               img[x, y] = pixel;
            }
         }
      }
      static byte EmbedBit(byte component, byte[] bytes, ref int byteIdx, ref int bitIdx)
      {
         int bit = (bytes[byteIdx] >> bitIdx) & 1;
         byte newComponent = (byte)((component & 0xFE) | bit);
         bitIdx++;
         if (bitIdx == 8) { bitIdx = 0; byteIdx++; }
         return newComponent;
      }
      static string ExtractMessage(Image<Rgba32> img)
      {
         System.Collections.Generic.List<byte> resultBytes = new();
         int currentByte = 0;
         int bitIndex = 0;

         for (int y = 0; y < img.Height; y++)
         {
            for (int x = 0; x < img.Width; x++)
            {
               Rgba32 pixel = img[x, y];
               byte[] components = { pixel.R, pixel.G, pixel.B };

               foreach (byte comp in components)
               {
                  int bit = comp & 1;
                  currentByte |= (bit << bitIndex);
                  bitIndex++;

                  if (bitIndex == 8)
                  {
                     if (currentByte == 0)
                        return Encoding.UTF8.GetString(resultBytes.ToArray());

                     resultBytes.Add((byte)currentByte);
                     currentByte = 0;
                     bitIndex = 0;
                  }
               }
            }
         }
         return Encoding.UTF8.GetString(resultBytes.ToArray());
      }
   }
}