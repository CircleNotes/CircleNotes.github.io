/* global hexo */
'use strict';

const { stripHTML, unescapeHTML } = require('hexo-util');

/** 统计可阅读正文：汉字逐字、英文及数字标识符逐词，排除代码与非正文内容。 */
function countArticle(content) {
  const proseHTML = String(content || '')
    .replace(/<!--[\s\S]*?-->/g, ' ')
    .replace(/<(script|style)\b[^>]*>[\s\S]*?<\/\1\s*>/gi, ' ')
    .replace(/<figure\b[^>]*\bclass\s*=\s*(["'])[^"']*\bhighlight\b[^"']*\1[^>]*>[\s\S]*?<\/figure\s*>/gi, ' ')
    .replace(/<pre\b[^>]*>[\s\S]*?<\/pre\s*>/gi, ' ')
    .replace(/<\/(?:p|div|li|h[1-6]|td|th|tr|section|article|blockquote)\s*>|<(?:br|hr)\b[^>]*>/gi, ' ');
  const text = unescapeHTML(stripHTML(proseHTML).replace(/&#(?:x([0-9a-f]+)|(\d+));/gi,
    (entity, hexadecimal, decimal) => {
      const codePoint = parseInt(hexadecimal || decimal, hexadecimal ? 16 : 10);
      return codePoint <= 0x10FFFF ? String.fromCodePoint(codePoint) : ' ';
    }).replace(/&(?:nbsp|ensp|emsp|thinsp);/gi, ' '));
  const chineseCharacters = (text.match(/\p{Script=Han}/gu) || []).length;
  const latinWords = (text.replace(/\p{Script=Han}/gu, ' ')
    .match(/[\p{Script=Latin}\p{Number}]+(?:[_.’'\-][\p{Script=Latin}\p{Number}]+)*/gu) || []).length;
  return { chineseCharacters, latinWords, total: chineseCharacters + latinWords };
}

/** 分别按中文每分钟300字、英文每分钟200词估算正文阅读时长。 */
function readingMinutes(stats) {
  return Math.max(1, Math.ceil(stats.chineseCharacters / 300 + stats.latinWords / 200));
}

/** 显示精确字词数，用千位分隔符提高可读性。 */
function formatCount(count) {
  return count.toLocaleString('en-US');
}

module.exports = { countArticle, readingMinutes, formatCount };

if (typeof hexo !== 'undefined') {
  // 所有主题脚本加载完成后覆盖 helper，避免主题加载顺序影响统计规则。
  hexo.extend.filter.register('after_init', function registerReadingStats() {
    hexo.extend.helper.register('wordcount', post =>
      formatCount(countArticle(post.origin || post.content).total));
    hexo.extend.helper.register('min2read', post =>
      readingMinutes(countArticle(post.origin || post.content)));
    hexo.extend.helper.register('wordtotal', site => {
      let total = 0;
      site.posts.forEach(post => { total += countArticle(post.origin || post.content).total; });
      return formatCount(total);
    });
  });
}
