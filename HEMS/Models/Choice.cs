using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HEMS.Models
{
    public class Choice
    {
        [Key]
        public int ChoiceId { get; set; }

        [Required]
        public string ChoiceText { get; set; } = string.Empty;

        public bool IsAnswer { get; set; } = false;

        // FK → Question
        public int QuestionId { get; set; }

        // Nullable navigation is correct for EF Core
        [ForeignKey(nameof(QuestionId))]
        public virtual Question? Question { get; set; }
    }
}
