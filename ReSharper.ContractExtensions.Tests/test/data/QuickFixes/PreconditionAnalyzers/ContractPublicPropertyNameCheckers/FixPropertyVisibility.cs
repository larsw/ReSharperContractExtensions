using System.Diagnostics.Contracts;

class A
{
  [ContractPublicPropertyName("IsValid")]
  private bool {caret}_isValid;
  private bool IsValid {get; set;}
}