# Circle 的技术博客

使用 Hexo + Fluid 构建，通过 GitHub Actions 发布到 GitHub Pages。

网站地址：<https://circle7991.github.io/>

## 本地预览

安装 Node.js 22 LTS 与 Git，在仓库目录运行：

```bash
npm ci
npm run server
```

## 写文章

在 `source/_posts/` 中新增 Markdown 文件，顶部填写文章信息：

```yaml
---
title: 文章标题
date: 2026-10-15 18:00:00
categories:
  - Unity打包
tags:
  - Unity
---
```

也可以运行 `npm run new -- "文章标题"` 创建文章。

## 发布

提交并推送到 `main` 分支后，Actions 自动构建和发布。仓库 Settings → Pages 的 Source 使用 GitHub Actions。

## 配置

- `_config.yml`：网站标题、地址、文章链接。
- `_config.fluid.yml`：Fluid 主题与导航。
- `source/about/index.md`：关于页面。
- `.github/workflows/pages.yml`：自动发布。

## 字数与阅读时长

全站统一统计正文中的汉字和英文/数字词组：一个汉字计一字，一个英文或数字词组计一词。保留行内技术标识符，排除代码块、文本示意图、代码行号、HTML 标签与标点，显示为精确的“字词”数量。

阅读时长为 `汉字数 / 300 + 英文及数字词数 / 200`，向上取整，最少显示约 1 分钟。该数字估算正文阅读时间，代码理解时间不额外计入。

实现位于 `scripts/reading-stats.js`，在主题脚本加载完成后统一覆盖字数、阅读时长及总字数 helper，不修改主题依赖。运行 `npm test` 验证统计规则，`npm run build` 构建两篇文章。

主题：[Fluid](https://github.com/fluid-dev/hexo-theme-fluid)。博客文章的转载授权请联系作者。
