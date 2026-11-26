import type * as React from "react"

export function isText(value: unknown): value is string {
  return typeof value === "string"
}

export function isRefCallback<T>(ref: React.Ref<T> | undefined): ref is React.RefCallback<T> {
  return typeof ref === "function"
}

export function isUpdater(value: boolean | ((previous: boolean) => boolean)): value is (previous: boolean) => boolean {
  return typeof value === "function"
}
