using MediatR;
using Nexora.Domain.Repositories;
using System.Threading;
using System.Threading.Tasks;

namespace Nexora.Application.Commands.Tenant
{
    public class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, long>
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateTenantCommandHandler(ITenantRepository tenantRepository, IUnitOfWork unitOfWork)
        {
            _tenantRepository = tenantRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<long> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
        {
            var tenant = new Nexora.Domain.Entities.Tenant(
                request.PropertyId,
                request.FirstName,
                request.LastName,
                request.Country,
                request.City,
                request.Address,
                request.PhoneNumber
            );

            await _tenantRepository.AddAsync(tenant);
            await _unitOfWork.SaveChangesAsync();

            return tenant.Id;
        }
    }

    public class UpdateTenantCommandHandler : IRequestHandler<UpdateTenantCommand, bool>
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateTenantCommandHandler(ITenantRepository tenantRepository, IUnitOfWork unitOfWork)
        {
            _tenantRepository = tenantRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepository.GetByIdAsync(request.TenantId);
            if (tenant == null) return false;

            tenant.UpdatePersonalInfo(
                request.FirstName,
                request.LastName,
                request.Country,
                request.City,
                request.Address,
                request.PhoneNumber
            );

            await _tenantRepository.UpdateAsync(tenant);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }
    }

    public class DeleteTenantCommandHandler : IRequestHandler<DeleteTenantCommand, bool>
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteTenantCommandHandler(ITenantRepository tenantRepository, IUnitOfWork unitOfWork)
        {
            _tenantRepository = tenantRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(DeleteTenantCommand request, CancellationToken cancellationToken)
        {
            var tenant = await _tenantRepository.GetByIdAsync(request.TenantId);
            if (tenant == null) return false;

            await _tenantRepository.DeleteAsync(tenant);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }
    }
}
