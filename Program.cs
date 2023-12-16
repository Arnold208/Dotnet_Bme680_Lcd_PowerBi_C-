using System;
using System.Device.I2c;
using System.Device.Gpio;
using System.Net.Http;
using System.Text;
using System.Threading;
using Iot.Device.Bmxx80;
using Iot.Device.Bmxx80.PowerMode;
using Iot.Device.CharacterLcd;
using Iot.Device.Pcx857x;
using Newtonsoft.Json;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Displaying current time. Press Ctrl+C to end.");

        var i2cSettings = new I2cConnectionSettings(1, 0x77);
        using I2cDevice i2cDevice = I2cDevice.Create(i2cSettings);
        using var bme680 = new Bme680(i2cDevice);

        var I2CSet = new I2cConnectionSettings(1, 0x27);
        using I2cDevice i2c = I2cDevice.Create(I2CSet);
        using var driver = new Pcf8574(i2c);
        using var lcd = new Lcd2004(registerSelectPin: 0,
                                   enablePin: 2,
                                   dataPins: new int[] { 4, 5, 6, 7 },
                                   backlightPin: 3,
                                   backlightBrightness: 0.1f,
                                   readWritePin: 1,
                                   controller: new GpioController(PinNumberingScheme.Logical, driver));

        UnitsNet.Duration measurementDuration = bme680.GetMeasurementDuration(Bme680HeaterProfile.Profile1);
        int measurementTime = (int)measurementDuration.Milliseconds;

        while (true)
        {
            Console.Clear();

            bme680.SetPowerMode(Bme680PowerMode.Forced);
            Thread.Sleep(measurementTime);

            bme680.TryReadTemperature(out var tempValue);
            bme680.TryReadPressure(out var preValue);
            bme680.TryReadHumidity(out var humValue);

            Console.WriteLine($"Temperature: {tempValue.DegreesCelsius:0.#}\u00B0C");
            Console.WriteLine($"Pressure: {preValue.Hectopascals:#.##} hPa");
            Console.WriteLine($"Relative humidity: {humValue.Percent:#.##}%");

             
            var telemetryData = new
            {
                Temperature = tempValue.DegreesCelsius,
                Humidity = humValue.Percent,
                Pressure = preValue.Hectopascals,
                Latitude = 5.567531010909944,  
                Longitude = -0.1920128916560704,
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")

                
            };

            var endpoint = "https://api.powerbi.com/beta/b17dbc4b-d422-4d8c-b388-d22cd859fdc5/datasets/bcf2a221-b556-4701-a76b-bbaa91d829e3/rows?experience=power-bi&key=tiyPf8rnm02MfmYpyLWd%2B5lXSRMdZ1l8iNCeebd%2BjfQTPaWXychnuTnZcpSpdwlhbeMrHtH8zcPEPIp%2FusZJVw%3D%3D";
            var content = new StringContent(JsonConvert.SerializeObject(new[] { telemetryData }), Encoding.UTF8, "application/json");

            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.PostAsync(endpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Data sent successfully!");
                }
                else
                {
                    Console.WriteLine($"Failed to send data. Status code: {response.StatusCode}");
                }
            }

            lcd.Clear();
            lcd.SetCursorPosition(0, 0);
            lcd.Write($"Temp: {tempValue.DegreesCelsius:0.#}\u00B0C");

            lcd.SetCursorPosition(0, 1);
            lcd.Write($"Humidity: {humValue.Percent:#.##}%");
        }
    }
}