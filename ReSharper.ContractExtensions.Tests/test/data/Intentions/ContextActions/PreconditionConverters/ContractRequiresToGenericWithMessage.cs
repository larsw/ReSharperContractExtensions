using System.Diagnostics.Contracts;

class A
{
  public void EnabledOnAbstractMethod(string s)
  {
    Contract.Requires{caret}(s != null && s.Length != 0, "s should not be null or empty");
  }
}