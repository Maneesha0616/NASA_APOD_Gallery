using System.ComponentModel.DataAnnotations;

namespace NasaApodGallery.Models;

public class FetchRequestViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Start date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Start Date")]
    public DateTime? StartDate { get; set; } = DateTime.Today.AddDays(-6);

    [Required(ErrorMessage = "End date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "End Date")]
    public DateTime? EndDate { get; set; } = DateTime.Today;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate.HasValue && EndDate.HasValue)
        {
            if (StartDate.Value.Date > EndDate.Value.Date)
            {
                yield return new ValidationResult(
                    "Start Date cannot be later than End Date.",
                    new[] { nameof(StartDate) });
            }

            var earliestNasaApodDate = new DateTime(1995, 6, 16);
            if (StartDate.Value.Date < earliestNasaApodDate)
            {
                yield return new ValidationResult(
                    $"Start Date cannot be earlier than {earliestNasaApodDate:yyyy-MM-dd} (first APOD).",
                    new[] { nameof(StartDate) });
            }
        }
    }
}

