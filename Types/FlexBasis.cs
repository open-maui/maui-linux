namespace Microsoft.Maui.Platform;

public struct FlexBasis
{
    public bool IsAuto { get; }

    public float Length { get; }

    public static FlexBasis Auto => new FlexBasis(isAuto: true, 0f);

    private FlexBasis(bool isAuto, float length)
    {
        IsAuto = isAuto;
        Length = length;
    }

    public static FlexBasis FromLength(float length)
    {
        return new FlexBasis(isAuto: false, length);
    }

    public static implicit operator FlexBasis(float length)
    {
        return FromLength(length);
    }
}
