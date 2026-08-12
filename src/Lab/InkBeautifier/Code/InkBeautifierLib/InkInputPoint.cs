namespace InkBeautifierLib;

/// <summary>
/// 表示一个带时间和压感信息的输入采样点。
/// </summary>
public readonly record struct InkInputPoint(
    InkPoint2D Position,
    long Timestamp,
    double Pressure,
    long StrokeId,
    InkPointerStatus Status);