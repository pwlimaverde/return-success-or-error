namespace ReturnSuccessOrError;

/// <summary>
/// Contrato marcador para serviços de feature (Service Layer).
/// <para>
/// Implementações são o ponto de entrada público de uma feature, encapsulando
/// a orquestração de um ou mais <see cref="UsecaseBase{TValue}"/> /
/// <see cref="UsecaseBaseCallData{TValue, TData}"/>.
/// </para>
/// </summary>
public interface IFeatureService;
