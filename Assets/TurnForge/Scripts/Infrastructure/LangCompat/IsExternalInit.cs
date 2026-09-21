namespace System.Runtime.CompilerServices
{
    // Unityのターゲットランタイム(.NET Standard 2.1等)にはC# 9のinitアクセサが
    // 要求するこの型が含まれていないため、コンパイル用に自前で定義する。
    internal static class IsExternalInit
    {
    }
}
