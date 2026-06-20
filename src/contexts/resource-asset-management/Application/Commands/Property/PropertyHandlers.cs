using MediatR;
using Nexora.Domain.Entities;
using Nexora.Domain.Repositories;
using Nexora.Domain.Services;
using System.Threading;
using System.Threading.Tasks;

namespace Nexora.Application.Commands.Property
{
    public class CreatePropertyCommandHandler : IRequestHandler<CreatePropertyCommand, long>
    {
        private readonly IPropertyRepository _propertyRepository;
        private readonly ILandlordRepository _landlordRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly ISubscriptionPolicy _subscriptionPolicy;
        private readonly Nexora.Domain.Services.IPropertyCodeGenerator _propertyCodeGenerator;
        private readonly IUnitOfWork _unitOfWork;

        public CreatePropertyCommandHandler(
            IPropertyRepository propertyRepository, 
            ILandlordRepository landlordRepository, 
            ISubscriptionRepository subscriptionRepository,
            ISubscriptionPolicy subscriptionPolicy,
            IUnitOfWork unitOfWork,
            Nexora.Domain.Services.IPropertyCodeGenerator propertyCodeGenerator)
        {
            _propertyRepository = propertyRepository;
            _landlordRepository = landlordRepository;
            _subscriptionRepository = subscriptionRepository;
            _subscriptionPolicy = subscriptionPolicy;
            _unitOfWork = unitOfWork;
            _propertyCodeGenerator = propertyCodeGenerator;
        }

        public async Task<long> Handle(CreatePropertyCommand request, CancellationToken cancellationToken)
        {
            var landlord = await _landlordRepository.GetByUserIdAsync(request.UserId);
            if (landlord == null)
                throw new Exception("Landlord profile not found.");

            var subscription = await _subscriptionRepository.GetByLandlordIdAsync(landlord.Id);
            if (subscription != null && !_subscriptionPolicy.CanCreateProperty(subscription, landlord.Properties.Count))
            {
                var limit = subscription.Plan.UnlimitedProperties ? -1 : subscription.Plan.MaxPropertiesLimit;
                throw new Exception($"Subscription plan limit reached ({(limit == -1 ? "unlimited" : limit.ToString())}). Cannot add more properties.");
            }

            var code = await _propertyCodeGenerator.GenerateAsync(request.Type);

            var property = new Nexora.Domain.Entities.Property(
                request.Name,
                landlord.Id,
                request.Type,
                request.Country,
                request.City,
                request.Address,
                code,
                request.Description
            );

            if (request.IsSecurityModeArmed)
            {
                property.SetSecurityMode(true);
            }

            await _propertyRepository.AddAsync(property);
            await _unitOfWork.SaveChangesAsync();

            return property.Id;
        }
    }

    public class UpdatePropertyStatusHandler : IRequestHandler<UpdatePropertyStatusCommand, bool>
    {
        private readonly IPropertyRepository _propertyRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdatePropertyStatusHandler(IPropertyRepository propertyRepository, IUnitOfWork unitOfWork)
        {
            _propertyRepository = propertyRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(UpdatePropertyStatusCommand request, CancellationToken cancellationToken)
        {
            var property = await _propertyRepository.GetByIdAsync(request.PropertyId);
            if (property == null) return false;

            property.UpdateStatus(request.NewStatus);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }
    }

    public class UpdatePropertyCommandHandler : IRequestHandler<UpdatePropertyCommand, bool>
    {
        private readonly IPropertyRepository _propertyRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdatePropertyCommandHandler(IPropertyRepository propertyRepository, IUnitOfWork unitOfWork)
        {
            _propertyRepository = propertyRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> Handle(UpdatePropertyCommand request, CancellationToken cancellationToken)
        {
            var property = await _propertyRepository.GetByIdAsync(request.PropertyId);
            if (property == null) return false;

            property.Update(
                request.Name,
                request.Description,
                request.Type,
                request.Country,
                request.City,
                request.Address,
                request.Status
            );
            property.SetSecurityMode(request.IsSecurityModeArmed);

            await _unitOfWork.SaveChangesAsync();
            return true;
        }
    }
}
