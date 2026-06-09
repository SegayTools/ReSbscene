namespace SbScene.Core.Vtbf;

/// <summary>
/// 表示 VTBF 解析失败时抛出的异常，用于把格式错误和偏移诊断返回给调用方。
/// </summary>
public sealed class VtbfParseException : Exception
{
    /// <summary>
    /// 初始化VTBFParseException 实例，并保存调用方提供的核心数据。
    /// </summary>
    /// <param name="message">描述 VTBF 解析失败原因的错误消息。</param>
    public VtbfParseException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 初始化VTBFParseException 实例，并保存调用方提供的核心数据。
    /// </summary>
    /// <param name="message">描述 VTBF 解析失败原因的错误消息。</param>
    /// <param name="innerException">导致解析失败的底层异常。</param>
    public VtbfParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
