using Application.Interfaces;
using Application.Services;
using Infrastructure.Services.Payment;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.DI;

// ── Factory ──

public class PaymentGatewayFactory : IPaymentGatewayFactory
{
	private readonly IReadOnlyDictionary<string, IPaymentGatewayService> _gateways;
	private readonly ILogger<PaymentGatewayFactory> _logger;

	public PaymentGatewayFactory(
		IEnumerable<IPaymentGatewayService> gateways,
		ILogger<PaymentGatewayFactory> logger)
	{
		_logger = logger;
		_gateways = gateways.ToDictionary(
			g => g.GatewayName.ToUpper(),
			StringComparer.OrdinalIgnoreCase);
	}

	public IReadOnlyList<string> AvailableGateways =>
		_gateways.Keys.ToList().AsReadOnly();

	public IPaymentGatewayService GetGateway(string gatewayName)
	{
		if (_gateways.TryGetValue(gatewayName.ToUpper(), out var gateway))
			return gateway;

		_logger.LogError("[Factory] Gateway '{Gateway}' không tồn tại. Available={Available}",
			gatewayName, string.Join(", ", AvailableGateways));

		throw new NotSupportedException(
			$"Cổng '{gatewayName}' chưa hỗ trợ. Có sẵn: {string.Join(", ", AvailableGateways)}");
	}
}