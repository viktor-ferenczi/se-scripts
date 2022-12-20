#!/usr/bin/python3
# -*- coding: ascii -*-
"""Merges a Space Engineers program from multiple .cs source files into a single one
"""
import os
import re
import shutil
from typing import List, Set, Iterator

KEEP_USING_STATEMENTS = False
FLATTEN_INDENTATION = False
HEADER_COMMENT_PREFIX = '//>> '

CWD = os.getcwd()
SOURCE_FOLDER_NAME = os.path.split(CWD)[1]
PROGRAMS_DIR = os.path.join(os.getenv('APPDATA'), 'SpaceEngineers', 'Programs')

RX_MAIN_CLASS = re.compile(r'.*?class\s+[_a-zA-Z]+\s*:\s*MyGridProgram.*')


class Source:

    def __init__(self, path) -> None:
        self.path: str = path
        self.using_lines: List[str] = []
        self.code_lines: List[str] = []
        self.is_main = False
        self.namespace = ''
        self.read_source()
        self.is_valid: bool = self.code_lines and self.code_lines[0].strip() != '#if false'

    def read_source(self) -> None:
        with open(self.path, 'rt', encoding='utf-8-sig') as f:
            for line in f:
                line = line.rstrip()
                stripped_line = line.lstrip()
                if not stripped_line:
                    continue

                if stripped_line.startswith('using '):
                    self.using_lines.append(line)
                    continue

                self.code_lines.append(line.rstrip())

                if stripped_line.startswith('namespace '):
                    if not self.namespace:
                        self.namespace = stripped_line[10:].strip()
                    continue

                if RX_MAIN_CLASS.match(line) is not None:
                    self.is_main = True


class Converter:

    def __init__(self, folder: str, name: str):
        self.folder = folder
        self.name = name

        self.sources: List[Source] = self.load_source_files()
        self.sources = [source for source in self.sources if source.is_valid]
        if not self.sources:
            raise ValueError(f'No valid .cs source files found in {self.folder}')

        main_sources: List[Source] = [source for source in self.sources if source.is_main]
        if len(main_sources) != 1:
            raise ValueError('Exactly one source file must contain a class inherited from MyGridProgram')

        self.main_source: Source = main_sources[0]

        self.using_namespaces: Set[str] = set()
        for source in self.sources:
            self.using_namespaces.update(line for line in source.using_lines if '=' not in line)

        self.type_aliases: Set[str] = set()
        for source in self.sources:
            self.type_aliases.update(line for line in source.using_lines if '=' in line)

    def load_source_files(self) -> List[Source]:
        return [
            Source(os.path.join(self.folder, filename))
            for filename in sorted(os.listdir(self.folder))
            if filename.endswith('.cs')
        ]

    def write_program(self, output_path: str) -> None:
        with open(output_path, 'wt', encoding='utf-8') as f:
            print(f'{HEADER_COMMENT_PREFIX}{self.name}', file=f)

            if KEEP_USING_STATEMENTS:

                for line in sorted(self.using_namespaces):
                    print(line, file=f)
                if self.using_namespaces:
                    print(file=f)

                for line in sorted(self.type_aliases):
                    print(line, file=f)
                if self.type_aliases:
                    print(file=f)

            for source in self.sources:
                if source.is_main:
                    continue
                    
                write_source(f, source.code_lines)
                print(file=f)
                
            write_source(f, self.main_source.code_lines)


def strip_top_namespace(lines):
    if lines[0].lstrip().startswith('namespace ') and lines[1] == '{' and lines[-1] == '}':
        del lines[:2]
        del lines[-1]


def strip_program_class(lines):
    if lines[0].lstrip().endswith(': MyGridProgram') and lines[1] == '{' and lines[-1] == '}':
        del lines[:2]
        del lines[-1]


def strip_empty_lines(lines: List[str]) -> None:
    while lines and not lines[0].lstrip():
        del lines[0]

    while lines and not lines[-1].lstrip():
        lines.pop()


def remove_indentation(lines: List[str]) -> List[str]:
    indent_size = measure_indentation(lines)
    if indent_size == 0:
        return lines
    return [line[indent_size:] for line in lines]


def measure_indentation(lines: List[str]) -> int:
    if not lines:
        return 0

    return min((len(line) - len(line.lstrip())) for line in lines if line.lstrip())


def flatten_indentation(lines: List[str]) -> List[str]:
    return [line.lstrip() for line in lines]


def remove_comments(lines: List[str]) -> Iterator[str]:
    multiline_comment = False
    for line in lines:
        if multiline_comment:
            if line.endswith('*/'):
                multiline_comment = False
        else:
            sl = line.lstrip()
            if sl.startswith('/*'):
                multiline_comment = True
            elif not sl.startswith('//'):
                yield line


def write_source(f, lines):
    lines = list(remove_comments(lines))
    strip_top_namespace(lines)
    lines = remove_indentation(lines)
    strip_program_class(lines)
    lines = remove_indentation(lines)

    if FLATTEN_INDENTATION:
        lines = flatten_indentation(lines)

    for line in lines:
        print(line, file=f)


def main() -> None:
    os.makedirs(PROGRAMS_DIR, exist_ok=True)
    path = os.path.join(PROGRAMS_DIR, f'{SOURCE_FOLDER_NAME}.cs')
    new_path = f'{path}.new'
    converter = Converter(CWD, SOURCE_FOLDER_NAME)
    converter.write_program(new_path)
    shutil.move(new_path, path)


if __name__ == '__main__':
    main()