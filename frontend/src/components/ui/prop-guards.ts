import type * as React from "react"

export function isText<T>(value: T): value is Extract<T, string> {
  return typeof value === "string"
}

export function isRefCallback<T>(ref: React.Ref<T> | undefined): ref is React.RefCallback<T> {
  return typeof ref === "function"
}

export function isUpdater<T>(value: T | ((previous: T) => T)): value is (previous: T) => T {
  return typeof value === "function"
}
