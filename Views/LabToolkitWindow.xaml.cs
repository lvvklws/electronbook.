using System;
using System.Windows;

namespace Computer_networks.Views
{
    public partial class LabToolkitWindow : Window
    {
        private readonly Random _random = new Random();

        public LabToolkitWindow()
        {
            InitializeComponent();
            CalculateAndRender();
        }

        private void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            CalculateAndRender();
        }

        private void GenerateVariantButton_Click(object sender, RoutedEventArgs e)
        {
            int[] prefixes = { 24, 25, 26, 27, 28, 29, 30 };
            int first = 10;
            int second = _random.Next(0, 256);
            int third = _random.Next(0, 256);
            int fourth = _random.Next(1, 255);
            int prefix = prefixes[_random.Next(prefixes.Length)];

            IpInputTextBox.Text = $"{first}.{second}.{third}.{fourth}";
            PrefixInputTextBox.Text = prefix.ToString();

            CalculateAndRender();
            StatusText.Text = "Сгенерирован тренировочный вариант для лабораторной работы.";
        }

        private void CalculateAndRender()
        {
            if (!TryParseIpv4(IpInputTextBox.Text, out uint ipAddress))
            {
                StatusText.Text = "Ошибка: введите корректный IPv4 адрес в формате A.B.C.D.";
                return;
            }

            if (!int.TryParse(PrefixInputTextBox.Text, out int prefix) || prefix < 0 || prefix > 32)
            {
                StatusText.Text = "Ошибка: префикс CIDR должен быть числом от 0 до 32.";
                return;
            }

            uint mask = PrefixToMask(prefix);
            uint wildcardMask = ~mask;
            uint networkAddress = ipAddress & mask;
            uint broadcastAddress = networkAddress | wildcardMask;

            ulong hostCount;
            string firstHost;
            string lastHost;
            if (prefix == 32)
            {
                hostCount = 1;
                firstHost = UIntToIp(networkAddress);
                lastHost = UIntToIp(networkAddress);
            }
            else if (prefix == 31)
            {
                hostCount = 2;
                firstHost = UIntToIp(networkAddress);
                lastHost = UIntToIp(broadcastAddress);
            }
            else
            {
                hostCount = (1UL << (32 - prefix)) - 2UL;
                firstHost = UIntToIp(networkAddress + 1);
                lastHost = UIntToIp(broadcastAddress - 1);
            }

            string classText = GetAddressClass(ipAddress);

            NetworkAddressText.Text = $"Адрес сети: {UIntToIp(networkAddress)}/{prefix}";
            SubnetMaskText.Text = $"Маска подсети: {UIntToIp(mask)}";
            WildcardMaskText.Text = $"Wildcard mask: {UIntToIp(wildcardMask)}";
            BroadcastAddressText.Text = $"Broadcast: {UIntToIp(broadcastAddress)}";
            FirstHostText.Text = $"Первый хост: {firstHost}";
            LastHostText.Text = $"Последний хост: {lastHost}";
            HostCountText.Text = $"Количество хостов: {hostCount}";
            AddressClassText.Text = $"Класс адреса: {classText}";

            ReportTextBox.Text =
                "Лабораторная работа: расчет параметров IPv4 подсети" + Environment.NewLine +
                $"Исходные данные: {UIntToIp(ipAddress)}/{prefix}" + Environment.NewLine +
                $"Адрес сети: {UIntToIp(networkAddress)}" + Environment.NewLine +
                $"Маска подсети: {UIntToIp(mask)}" + Environment.NewLine +
                $"Wildcard mask: {UIntToIp(wildcardMask)}" + Environment.NewLine +
                $"Broadcast-адрес: {UIntToIp(broadcastAddress)}" + Environment.NewLine +
                $"Диапазон хостов: {firstHost} - {lastHost}" + Environment.NewLine +
                $"Допустимое число хостов: {hostCount}" + Environment.NewLine +
                $"Класс IP-адреса: {classText}" + Environment.NewLine +
                "Вывод: параметры подсети рассчитаны корректно, сеть готова к использованию в лабораторной конфигурации.";

            StatusText.Text = "Расчет выполнен успешно.";
        }

        private static uint PrefixToMask(int prefix)
        {
            if (prefix == 0)
            {
                return 0;
            }

            return uint.MaxValue << (32 - prefix);
        }

        private static string UIntToIp(uint value)
        {
            return string.Format("{0}.{1}.{2}.{3}",
                (value >> 24) & 0xFF,
                (value >> 16) & 0xFF,
                (value >> 8) & 0xFF,
                value & 0xFF);
        }

        private static bool TryParseIpv4(string ipText, out uint ipValue)
        {
            ipValue = 0;
            if (string.IsNullOrWhiteSpace(ipText))
            {
                return false;
            }

            string[] parts = ipText.Split('.');
            if (parts.Length != 4)
            {
                return false;
            }

            uint result = 0;
            for (int i = 0; i < 4; i++)
            {
                if (!byte.TryParse(parts[i], out byte octet))
                {
                    return false;
                }

                result = (result << 8) | octet;
            }

            ipValue = result;
            return true;
        }

        private static string GetAddressClass(uint ipValue)
        {
            int firstOctet = (int)((ipValue >> 24) & 0xFF);

            if (firstOctet >= 1 && firstOctet <= 126)
            {
                return "A";
            }

            if (firstOctet >= 128 && firstOctet <= 191)
            {
                return "B";
            }

            if (firstOctet >= 192 && firstOctet <= 223)
            {
                return "C";
            }

            if (firstOctet >= 224 && firstOctet <= 239)
            {
                return "D (multicast)";
            }

            if (firstOctet >= 240 && firstOctet <= 255)
            {
                return "E (experimental)";
            }

            return "Специальный";
        }
    }
}
