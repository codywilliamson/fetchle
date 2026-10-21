# Third-party notices

fetchle ships with an embedding model next to the executable, `potion-retrieval-32M.fmodel`. The file is a converted copy of the model below (weights quantized to int8, vocabulary re-laid out for memory mapping); `tools/model/convert.cs` makes it from the pinned source files. fetchle's own code that runs the model is original and MIT licensed (see `LICENSE`).

## potion-retrieval-32M

- Source: https://huggingface.co/minishlab/potion-retrieval-32M, revision `6fc8051fab2a1e0ee76689cf08c853792ac285e7`
- Authors: Minish Lab (Stephan Tulkens, Thomas van Dongen)
- License: MIT, as declared in the model card. The model repository carries no separate license file or copyright line, so the MIT text below is the one published for the Model2Vec project that produced it.
- Lineage, from the model card: a finetune of `minishlab/potion-base-32M` (MIT), which was distilled with Model2Vec from `BAAI/bge-base-en-v1.5` (MIT).

## Model2Vec

The inference method (WordPiece tokenization, mean of static token embeddings, L2 normalization) and the model family come from Model2Vec. The model card asks users to cite it:

```bibtex
@software{minishlab2024model2vec,
  author       = {Stephan Tulkens and {van Dongen}, Thomas},
  title        = {Model2Vec: Fast State-of-the-Art Static Embeddings},
  year         = {2024},
  publisher    = {Zenodo},
  doi          = {10.5281/zenodo.17270888},
  url          = {https://github.com/MinishLab/model2vec},
  license      = {MIT}
}
```

Model2Vec is licensed as follows (https://github.com/MinishLab/model2vec/blob/main/LICENSE):

```text
MIT License

Copyright (c) 2024 Thomas van Dongen

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
