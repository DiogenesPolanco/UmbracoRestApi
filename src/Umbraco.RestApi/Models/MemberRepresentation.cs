using System.ComponentModel.DataAnnotations;

namespace Umbraco.RestApi.Models
{
    public class MemberRepresentation : ContentRepresentationBase
    {
       
        [Required]
        [Display(Name = "userName")]
        public string UserName { get; set; }

        [Required]
        [Display(Name = "email")]
        public string Email { get; set; }
        
        protected override void CreateHypermedia()
        {
            base.CreateHypermedia();

            //required link to self
            Href = LinkTemplates.Members.Self.CreateLink(new { id = Key }).Href;
            Rel = LinkTemplates.Members.Self.Rel;

            Links.Add(LinkTemplates.Members.Root);
            Links.Add(LinkTemplates.Members.MetaData.CreateLink(new { id = Key }));
        }
    }
}