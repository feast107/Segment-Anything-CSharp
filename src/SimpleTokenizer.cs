using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SAMViewer
{
    /// <summary>
    /// BPE
    /// </summary>
    internal class SimpleTokenizer
    {
        private static SimpleTokenizer                   theSingleton = null;
        private        Dictionary<int, string>           byte_encoder;
        private        Dictionary<string, int>           byte_decoder;
        private        Dictionary<string, int>           encoder   = new Dictionary<string, int>();
        private        Dictionary<int, string>           decoder   = new Dictionary<int, string>();
        private        Dictionary<(string, string), int> bpe_ranks = new Dictionary<(string, string), int>();
        private        Dictionary<string, string>        cache     = new Dictionary<string, string>();
        private        Regex                             pat;
        private        int                               contextLength = 77;

        public static SimpleTokenizer Instance()
        {
            if (null == theSingleton)
            {
                theSingleton = new SimpleTokenizer();
            }
            return theSingleton;
        }

        protected SimpleTokenizer()
        {
            Init();
        }
        /// <summary>
        /// 初始化
        /// </summary>
        private void Init()
        {
            byte_encoder = BytesToUnicode();
            byte_decoder = byte_encoder.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

            var merges = LoadBPEMerges(default_bpe());//加载BPE
            var vocab = byte_encoder.Values.ToList();
            foreach (var v in byte_encoder.Values.ToList())
            {
                vocab.Add(v + "</w>");
            }


            foreach ((var merge1, var merge2) in merges)
            {
                vocab.Add(merge1 + merge2);
            }
            vocab.AddRange(new List<string> { "<|startoftext|>", "<|endoftext|>" });

            for (var i = 0; i < vocab.Count; i++)
            {
                encoder[vocab[i]] = i;
            }
            decoder = encoder.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
            bpe_ranks = merges.Select((merge, index) => new { merge, index })
                                         .ToDictionary(item => item.merge, item => item.index);
            cache = new Dictionary<string, string>()
            {
                { "<|startoftext|>","<|startoftext|>" },
                { "<|endoftext|>","<|endoftext|>" }
            };

            pat = new Regex(@" <\| startoftext\|>|<\| endoftext\|>| 's|'t | 're|'ve | 'm|'ll | 'd|[\p{L}]+|[\p{N}]|[^\s\p{L}\p{N}]+", RegexOptions.IgnoreCase);
        }
        /// <summary>
        /// 将字符串转换为Token
        /// </summary>
        /// <param name="textpromot"></param>
        /// <returns></returns>
        public List<Int64> tolikenlize(string textpromot)
        {
            var sot_token = encoder["<|startoftext|>"];
            var eot_token = encoder["<|endoftext|>"];
            var texts = new List<string>() { textpromot };
            var allTokens = new List<Int64>();
            foreach (var text in texts)
            {
                allTokens.Add(sot_token);
                allTokens.AddRange(Encode(text));
                allTokens.Add(eot_token);
            }
            if (allTokens.Count > contextLength)
            {
                allTokens.RemoveRange(contextLength, allTokens.Count - contextLength);
                allTokens[contextLength-1] = eot_token;
            }
            else
            {
                var added = new Int64[contextLength - allTokens.Count];
                allTokens.AddRange(added);
            }

            return allTokens;
        }
        /// <summary>
        /// 对字符串进行编码
        /// </summary>
        private List<Int64> Encode(string text)
        {
            var bpeTokens = new List<Int64>();
            text = whitespace_clean(basic_clean(text)).ToLower();
            foreach (Match match in Regex.Matches(text, pat.ToString()))
            {
                var token = string.Join("", match.Value.Select(c => byte_encoder[c]));
                var bpeTokenList = bpe(token).Split(' ');
                foreach (var bpeToken in bpeTokenList)
                {
                    bpeTokens.Add(encoder[bpeToken]);
                }
            }
            return bpeTokens;
        }
        /// <summary>
        /// 将tokens解码成字符串解码
        /// </summary>
        private string Decode(List<int> tokens)
        {
            var textBuilder = new StringBuilder();
            foreach (var token in tokens)
            {
                textBuilder.Append(decoder[token]);
            }
            var text = textBuilder.ToString();

            var byteList = new List<byte>();
            foreach (var c in text)
            {
                byteList.Add((byte)byte_decoder[c.ToString()]);
            }
            var byteArray = byteList.ToArray();
            var decodedText = Encoding.UTF8.GetString(byteArray).Replace("</w>", " ");

            return decodedText;
        }

        private string bpe(string token)
        {
            if (cache.ContainsKey(token))
            {
                return cache[token];
            }
            var word = new List<string>();
            for (var i=0;i< token.Length-1;i++)
            {
                word.Add(token[i].ToString());
            }
            word.Add(token[token.Length - 1].ToString() + "</w>");
            //Tuple<string, string> word = Tuple.Create( token[token.Length - 1] + "</w>", token.Substring(0, token.Length - 1));
            var pairs = GetPairs(word);

            if (pairs.Count == 0)
            {
                return token + "</w>";
            }

            while (true)
            {
                (var first, var second) = pairs.OrderBy(pair => bpe_ranks.ContainsKey(pair) ? bpe_ranks[pair] : double.PositiveInfinity).First();
                if (!bpe_ranks.ContainsKey((first, second)))
                {
                    break;
                }

                var newWord = new List<string>();
                var i = 0;
                while (i < word.Count)
                {
                    try
                    {
                        var j = word.IndexOf(first, i);
                        newWord.AddRange(word.GetRange(i, j - i));
                        i = j;
                    }
                    catch
                    {
                        newWord.AddRange(word.GetRange(i, word.Count- i));
                        break;
                    }

                    if (word[i] == first && i < word.Count - 1 && word[i + 1] == second)
                    {
                        newWord.Add(first + second);
                        i += 2;
                    }
                    else {
                        newWord.Add(word[i]);
                        i += 1;
                    }

                }
                word = newWord;
                if (word.Count == 1)
                    break;
                else
                {
                    pairs = GetPairs(newWord);
                }
               
            }

            var result = string.Join(" ", word);
            cache[token] = result;
            return result;

        }

        private string default_bpe()
        {
            return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)+ "\\bpe_simple_vocab_16e6.txt";
        }

        private List<(string, string)> LoadBPEMerges(string bpePath)
        {
            var merges = new List<(string, string)>();

            using (var fileStream = File.OpenRead(bpePath))
            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
            {
                var content = streamReader.ReadToEnd();
                var lines = content.Split('\n');
                var startLine = 1;
                var endLine = 49152 - 256 - 2 + 1;
                var lineSegment = new ArraySegment<string>(lines, startLine, endLine - startLine);

                foreach (var line in lineSegment)
                {
                    var merge = line.Split();
                    merges.Add((merge[0], merge[1]));
                }
            }

            return merges;
        }

        private Dictionary<int, string> BytesToUnicode()
        {
            var bs = new List<int>();
            var cs = new List<int>();

            for (var b = (int)'!'; b <= '~'; b++)
            {
                bs.Add(b);
                cs.Add(b);
            }

            for (var b = (int)'¡'; b <= '¬'; b++)
            {
                bs.Add(b);
                cs.Add(b);
            }

            for (var b = (int)'®'; b <= 'ÿ'; b++)
            {
                bs.Add(b);
                cs.Add(b);
            }

            var n = 0;
            for (var b = 0; b < 256; b++)
            {
                if (!bs.Contains(b))
                {
                    bs.Add(b);
                    cs.Add(256 + n);
                    n++;
                }
            }

            var byteToUnicode = new Dictionary<int, string>();
            for (var i = 0; i < bs.Count; i++)
            {
                byteToUnicode.Add(bs[i], ((char)cs[i]).ToString());
            }

            return byteToUnicode;
        }

        private HashSet<(string, string)> GetPairs(List<string> word)
        {
            var pairs = new HashSet<(string, string)>();
            var prevChar = word[0];
            for (var i = 1; i < word.Count; i++)
            {
                var currentChar = word[i].ToString();
                pairs.Add((prevChar, currentChar));
                prevChar = currentChar;
            }
            return pairs;
        }

        private string HtmlDecode(string text)
        {
            // 还原 HTML 转义字符
            return System.Net.WebUtility.HtmlDecode(text);
        }

        private string basic_clean(string text)
        {
            text = HtmlDecode(text);
            return text.Trim();
        }

        private string whitespace_clean(string text)
        {
            text = Regex.Replace(text, @"\s+", " ");
            text = text.Trim();
            return text;
        }
    }
}
