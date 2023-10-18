namespace Bookify.Web.Core.ViewModel
{
    public class CategoryFormViewModel
    {
        public int Id { get; set; }
        [MaxLength(100,ErrorMessage ="Max Length cannot be more than 5 chr"), Required]
        public string Name { get; set; } = null!;
    }
}
