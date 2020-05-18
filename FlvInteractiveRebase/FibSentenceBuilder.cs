using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FlvInteractiveRebase
{
    internal class FibSentenceBuilder : SentenceBuilder
    {
        internal static readonly FibSentenceBuilder Singleton = new FibSentenceBuilder();

        private FibSentenceBuilder()
        {
        }

        public override Func<string> RequiredWord => () => "必须.";

        public override Func<string> ErrorsHeadingText => () => "错误:";

        public override Func<string> UsageHeadingText => () => "用法:";

        public override Func<string> OptionGroupWord => () => "分组";

        public override Func<bool, string> HelpCommandText => isOption => isOption ? "显示帮助." : "显示一个命令的详细信息.";

        public override Func<bool, string> VersionCommandText => _ => "显示版本信息.";

        public override Func<Error, string> FormatError => error =>
        {
            return error.Tag switch
            {
                ErrorType.BadFormatTokenError => $"无法识别 '{((BadFormatTokenError)error).Token}'.",
                ErrorType.MissingValueOptionError => $"参数 '{((MissingValueOptionError)error).NameInfo.NameText}' 没有值.",
                ErrorType.UnknownOptionError => $"未知的参数 '{ ((UnknownOptionError)error).Token }'.",
                ErrorType.MissingRequiredOptionError => ((MissingRequiredOptionError)error).NameInfo.Equals(NameInfo.EmptyName) ? "缺少一个必须的参数." : $"没有传递必须的参数 '{((MissingRequiredOptionError)error).NameInfo.NameText }'.",
                ErrorType.BadFormatConversionError => ((BadFormatConversionError)error).NameInfo.Equals(NameInfo.EmptyName) ? "A value not bound to option name is defined with a bad format." : "Option '" + ((BadFormatConversionError)error).NameInfo.NameText + "' is defined with a bad format.",
                ErrorType.SequenceOutOfRangeError => ((SequenceOutOfRangeError)error).NameInfo.Equals(NameInfo.EmptyName) ? "A sequence value not bound to option name is defined with few items than equired." : "A sequence option '" + ((SequenceOutOfRangeError)error).NameInfo.NameText + "' is defined with fewer or more items than required.",
                ErrorType.BadVerbSelectedError => $"子命令 '{((BadVerbSelectedError)error).Token}' 不存在.",
                ErrorType.NoVerbSelectedError => "没选择子命令.",
                ErrorType.RepeatedOptionError => $"传递了多次参数 '{((RepeatedOptionError)error).NameInfo.NameText}'.",
                ErrorType.SetValueExceptionError => "设置参数值时发生错误 '" + (((SetValueExceptionError)error).NameInfo.NameText + "': ", ((SetValueExceptionError)error).Exception.Message),
                ErrorType.MissingGroupOptionError => $"至少要传递参数组 '{((MissingGroupOptionError)error).Group}' ({string.Join(", ", ((MissingGroupOptionError)error).Names.Select(n => n.NameText))}) 中的一个参数.",
                ErrorType.GroupOptionAmbiguityError => "Both SetName and Group are not allowed in option: (" + ((GroupOptionAmbiguityError)error).Option.NameText + ")",
                ErrorType.MultipleDefaultVerbsError => MultipleDefaultVerbsError.ErrorMessage,
                _ => throw new InvalidOperationException(),
            };
        };

        public override Func<IEnumerable<MutuallyExclusiveSetError>, string> FormatMutuallyExclusiveSetErrors => errors =>
        {
            var bySet = from e in errors
                        group e by e.SetName into g
                        select new { SetName = g.Key, Errors = g.ToList() };

            var msgs = bySet.Select(
                set =>
                {
                    var names = string.Join(
                        string.Empty,
                        (from e in set.Errors select "'" + e.NameInfo.NameText + "', ").ToArray());
                    var namesCount = set.Errors.Count();

                    var incompat = string.Join(
                        string.Empty,
                        (from x in
                             (from s in bySet where !s.SetName.Equals(set.SetName) from e in s.Errors select e)
                            .Distinct()
                         select "'" + x.NameInfo.NameText + "', ").ToArray());

                    return
                        new StringBuilder("参数")
                                .Append(": ")
                                .Append(names[0..^2])
                                .Append(" 不与: ")
                                .Append(incompat[0..^2])
                                .Append(" 兼容.")
                            .ToString();
                }).ToArray();
            return string.Join(Environment.NewLine, msgs);
        };
    }
}
