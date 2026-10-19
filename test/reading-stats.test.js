'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { countArticle, readingMinutes, formatCount } = require('../scripts/reading-stats');

test('汉字逐字计数，英文标识符与数字词组按词计数', () => {
  assert.deepEqual(countArticle('<p>中文 AnimatorControllerPlayable.SetFloat 123。</p>'), {
    chineseCharacters: 2,
    latinWords: 2,
    total: 4
  });
});

test('排除高亮代码、行号以及普通代码和文本示意图', () => {
  const html = '<p>正文 Hello</p>' +
    '<figure class="highlight csharp"><table><tr><td>123</td><td>代码 Foo Bar</td></tr></table></figure>' +
    '<pre><code>更多代码 Graph</code></pre><pre>图示 → Animator</pre>';
  assert.deepEqual(countArticle(html), { chineseCharacters: 2, latinWords: 1, total: 3 });
});

test('解码实体并忽略隐藏脚本、样式和注释', () => {
  const html = '<p>&#20013;&#25991; A&amp;B</p><!-- hidden -->' +
    '<script>hidden words</script><style>hidden words</style>';
  assert.deepEqual(countArticle(html), { chineseCharacters: 2, latinWords: 2, total: 4 });
});

test('支持十六进制字符和空格实体，忽略超出Unicode范围的字符', () => {
  assert.deepEqual(countArticle('<p>&#x4e2d; hello&nbsp;world &#999999999;</p>'), {
    chineseCharacters: 1, latinWords: 2, total: 3
  });
});

test('分隔相邻段落，但保留跨行内标签的完整单词', () => {
  assert.equal(countArticle('<p>hello</p><p>world</p><p>Play<strong>able</strong>Graph</p>').latinWords, 3);
});

test('保留行内代码中的技术词汇，支持拉丁字符和扩展汉字', () => {
  assert.deepEqual(countArticle('<p>𠀀 <code>_animGraph.Evaluate</code> café</p>'), {
    chineseCharacters: 1,
    latinWords: 2,
    total: 3
  });
});

test('阅读时间分别按中文300字及英文200词计算并向上取整', () => {
  assert.equal(readingMinutes({ chineseCharacters: 300, latinWords: 200 }), 2);
  assert.equal(readingMinutes({ chineseCharacters: 301, latinWords: 200 }), 3);
  assert.equal(readingMinutes({ chineseCharacters: 0, latinWords: 0 }), 1);
});

test('展示精确字词数，不用k进行四舍五入', () => {
  assert.equal(formatCount(5242), '5,242');
  assert.equal(formatCount(10295), '10,295');
});
