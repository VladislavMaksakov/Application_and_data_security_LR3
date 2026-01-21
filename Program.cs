using System;
using System.Text;
// Підключення бібліотеки ImageSharp для кросплатформенної роботи із зображеннями
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LsbSteganography
{
   class Program
   {
      static void Main(string[] args)
      {
         // --- Налаштування шляхів та даних ---
         // Шлях до оригінального файлу. УВАГА: Файл має існувати!
         string originalImagePath = "/Users/vladislavmaksakov/Desktop/Learning/БПД/LR3/Encode.png";
         // Шлях, куди збережеться результат
         string stegoImagePath = "stego_result.png";
         // Текст для приховування (містить кирилицю та спецсимволи)
         string secretMessage = "Максаков Владислав Олександрович, ДН:05.03.2004, 6.04.121.013.22.2";

         Console.WriteLine("=== LSB Стеганографія (ImageSharp) ===");

         try
         {
            // --- Етап 1: Завантаження та Кодування ---
            // Використовуємо 'using', щоб автоматично звільнити пам'ять після завершення блоку
            // Rgba32 означає формат пікселя: Red, Green, Blue, Alpha (по 8 біт на канал)
            using (Image<Rgba32> image = Image.Load<Rgba32>(originalImagePath))
            {
               Console.WriteLine($"[INFO] Оригінал завантажено: {image.Width}x{image.Height}");

               Console.WriteLine("=== Етап 2: Приховування даних ===");
               // Виклик основного методу приховування
               HideMessage(image, secretMessage);

               // ВАЖЛИВО: Зберігаємо у форматі PNG.
               // JPG не підходить, бо стиснення з втратами знищить наші біти!
               image.SaveAsPng(stegoImagePath);
               Console.WriteLine($"[SUCCESS] Файл збережено: {stegoImagePath}");
            }

            // --- Етап 2: Перевірка (Декодування) ---
            Console.WriteLine("\n=== Етап 3: Витягування даних ===");

            // Завантажуємо щойно створений файл, щоб переконатися, що дані збереглись
            using (Image<Rgba32> stegoImage = Image.Load<Rgba32>(stegoImagePath))
            {
               string extractedText = ExtractMessage(stegoImage);
               Console.WriteLine($"[RESULT] Розшифровано:\n{extractedText}");
            }

            // --- Етап 3: Аналіз файлів ---
            var file1 = new System.IO.FileInfo(originalImagePath);
            var file2 = new System.IO.FileInfo(stegoImagePath);
            // Різниця у розмірі може бути через різні алгоритми стиснення метаданих PNG,
            // але візуально картинки ідентичні.
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

      // --- Метод приховування повідомлення ---
      static void HideMessage(Image<Rgba32> img, string message)
      {
         // 1. Конвертуємо рядок у байти (UTF-8 підтримує укр. мову).
         // Додаємо "\0" (null-термінатор) в кінці. Це стоп-сигнал для декодера.
         byte[] messageBytes = Encoding.UTF8.GetBytes(message + "\0");

         int byteIndex = 0; // Індекс поточного байта повідомлення
         int bitIndex = 0;  // Індекс біта в цьому байті (0..7)

         // Проходимо по кожному пікселю зображення (по рядках і стовпцях)
         for (int y = 0; y < img.Height; y++)
         {
            for (int x = 0; x < img.Width; x++)
            {
               // Якщо все повідомлення записано - виходимо з методу
               if (byteIndex >= messageBytes.Length) return;

               // Отримуємо поточний піксель
               Rgba32 pixel = img[x, y];

               // Записуємо дані послідовно в канали R, G, B
               // Це дозволяє сховати 3 біти в одному пікселі

               // Канал RED
               pixel.R = EmbedBit(pixel.R, messageBytes, ref byteIndex, ref bitIndex);

               // Канал GREEN (перевіряємо, чи ще є дані для запису)
               if (byteIndex < messageBytes.Length)
                  pixel.G = EmbedBit(pixel.G, messageBytes, ref byteIndex, ref bitIndex);

               // Канал BLUE
               if (byteIndex < messageBytes.Length)
                  pixel.B = EmbedBit(pixel.B, messageBytes, ref byteIndex, ref bitIndex);

               // Записуємо змінений піксель назад у зображення
               img[x, y] = pixel;
            }
         }
      }

      // --- Допоміжний метод для вбудовування 1 біта ---
      // ref int byteIdx, ref int bitIdx - передаються за посиланням, щоб змінювати лічильники
      static byte EmbedBit(byte component, byte[] bytes, ref int byteIdx, ref int bitIdx)
      {
         // 1. Витягуємо потрібний біт з байта повідомлення
         // зсуваємо байт праворуч і беремо останній біт (& 1)
         int bit = (bytes[byteIdx] >> bitIdx) & 1;

         // 2. Модифікуємо компонент кольору (R, G або B)
         // (component & 0xFE) -> 11111110 -> обнуляє останній біт (LSB) пікселя
         // | bit              -> записує наш біт (0 або 1) на це місце
         byte newComponent = (byte)((component & 0xFE) | bit);

         // 3. Переходимо до наступного біта
         bitIdx++;
         if (bitIdx == 8) // Якщо пройшли всі 8 біт байта
         {
            bitIdx = 0;   // Скидаємо лічильник бітів
            byteIdx++;    // Переходимо до наступного символу (байта)
         }
         return newComponent;
      }

      // --- Метод витягування повідомлення ---
      static string ExtractMessage(Image<Rgba32> img)
      {
         System.Collections.Generic.List<byte> resultBytes = new();
         int currentByte = 0; // Тут будемо збирати біти назад у байт
         int bitIndex = 0;

         // Проходимо по пікселях у тому ж порядку, що і при запису
         for (int y = 0; y < img.Height; y++)
         {
            for (int x = 0; x < img.Width; x++)
            {
               Rgba32 pixel = img[x, y];
               // Масив компонентів для зручного перебору
               byte[] components = { pixel.R, pixel.G, pixel.B };

               foreach (byte comp in components)
               {
                  // 1. Витягуємо LSB (останній біт) з кольору
                  int bit = comp & 1;

                  // 2. Додаємо цей біт у наш поточний байт на правильну позицію
                  currentByte |= (bit << bitIndex);

                  bitIndex++;

                  // 3. Якщо зібрали повний байт (8 біт)
                  if (bitIndex == 8)
                  {
                     // Перевіряємо на стоп-символ (Null terminator)
                     if (currentByte == 0)
                        // Перетворюємо зібрані байти назад у текст і повертаємо результат
                        return Encoding.UTF8.GetString(resultBytes.ToArray());

                     // Якщо не кінець - додаємо байт у список і скидаємо лічильники
                     resultBytes.Add((byte)currentByte);
                     currentByte = 0;
                     bitIndex = 0;
                  }
               }
            }
         }
         // Якщо стоп-символ не знайдено, повертаємо все, що прочитали
         return Encoding.UTF8.GetString(resultBytes.ToArray());
      }
   }
}