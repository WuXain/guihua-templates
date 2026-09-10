using Riok.Mapperly.Abstractions;
using YOU.Api.Models;
using YOU.Core.Features.RuntimeStatus;

namespace YOU.Api.Mapping;

/// <summary>
/// 定义演示接口的模型映射；Mapperly 在编译时生成实现，不使用运行时反射。
/// </summary>
[Mapper]
public static partial class DemoMapper
{
    /// <summary>
    /// 将核心层运行状态转换为 API 响应模型。
    /// </summary>
    /// <param name="source">核心层返回的运行状态，不能为 <see langword="null"/>。</param>
    /// <returns>仅包含 API 对外字段的响应模型。</returns>
    public static partial RuntimeStatusResponse ToResponse(RuntimeStatus source);
}
