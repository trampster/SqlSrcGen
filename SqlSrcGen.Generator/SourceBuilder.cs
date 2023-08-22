using System.Text;

namespace SqlSrcGen.Generator
{
    public class SourceBuilder
    {
        readonly StringBuilder _builder = new StringBuilder();

        string _indent = "";

        public void AppendLine(string line) => _builder.AppendLine($"{_indent}{line}");

        public void IncreaseIndent()
        {
            _indent += "    ";
        }

        public void AppendLine() => _builder.AppendLine();

        public void Append(string value) => _builder.Append(value);

        public void AppendStart(string value) => _builder.Append($"{_indent}{value}");

        public void DecreaseIndent()
        {
            if (_indent.Length >= 4)
            {
                _indent = _indent.Substring(0, _indent.Length - 4);
            }
        }

        public override string ToString() => _builder.ToString();
    }
}