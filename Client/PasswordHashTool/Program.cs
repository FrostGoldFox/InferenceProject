using inferenceclinet.Services;

Console.WriteLine("문자열을 입력하면 salt 없이 SHA-256 해시값을 출력합니다. (종료: 빈 줄 입력 후 Enter)");

while (true)
{
    Console.Write("입력> ");
    var input = Console.ReadLine();

    if (string.IsNullOrEmpty(input))
    {
        break;
    }

    Console.WriteLine(PasswordHasher.Hash(input));
}
